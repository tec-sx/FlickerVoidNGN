using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Maps.Definitions;
using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>
/// The maps and markers of the world (a fixed system and service, ported from FVNavigationSystem): markers on entities and at places,
/// their visibility on fact changes, discovery, the tracked marker and the player's waypoint; and the navigator's map underfoot, the
/// places it discovers on its checks, and the minimap, compass and world map views. Headings are degrees clockwise from north, -Z.
/// </summary>
public sealed class MapSystem : BaseSystem<World, GameTime>
{
    /// <summary>Metres past a view's range a marker still counts as near, so views stay right while the navigator moves between checks.</summary>
    private const float NearbyMargin = 20;

    private readonly EventBus _events;
    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly MapSettings _settings;
    private readonly Dictionary<int, Marker> _markers = [];
    private readonly Dictionary<Entity, MarkerHandle> _entityMarkers = [];
    private readonly List<MarkerHandle> _nearby = [];
    private IReadOnlyList<MapDefinition>? _maps;
    private int _nextId;
    private float _sinceUpdate;

    public MapSystem(Game game, MapSettings settings)
        : base(game.World)
    {
        _events = game.Events;
        _facts = game.Facts;
        _services = game.Services;
        _settings = settings;
        game.Lifecycle.OnActivate((Entity entity, ref MapMarker marker) => Register(entity, marker));
        game.Lifecycle.OnDeactivate((Entity entity, ref MapMarker _) => Unregister(entity));
        game.Lifecycle.OnActivate((Entity entity, ref Navigator _) => SetNavigator(entity));
        game.Lifecycle.OnDeactivate((Entity entity, ref Navigator _) => ClearNavigator(entity));
        _facts.FactChanged += HandleFactChanged;
        game.SaveStates.Add<SavedMarker>("mapMarker", SaveMarker, RestoreMarker);
        game.SaveStates.AddGlobal<SavedNavigation>("navigation", SaveNavigation, RestoreNavigation);
    }

    /// <summary>The navigator the views follow; its entity is the instigator of marker conditions and the discoverer.</summary>
    public Entity Navigator { get; private set; } = Entity.Null;

    public MapDefinition? ActiveMap { get; private set; }

    public int ActiveLayer { get; private set; } = -1;

    public MarkerHandle Tracked { get; private set; } = MarkerHandle.None;

    public MarkerHandle Waypoint { get; private set; } = MarkerHandle.None;

    /// <summary>Every map of the content folder.</summary>
    public IReadOnlyList<MapDefinition> Maps
    {
        get
        {
            if (_maps is null)
            {
                DataAssets? data = _services.GetService<DataAssets>();
                if (data is null)
                {
                    return [];
                }

                _maps = data.LoadAll<MapDefinition>();
            }

            return _maps;
        }
    }

    public IReadOnlyList<MarkerHandle> Markers => [.. _markers.Values.Select(marker => marker.Handle)];

    public override void Update(in GameTime t)
    {
        _sinceUpdate += (float)t.Elapsed.TotalSeconds;
        if (_sinceUpdate < _settings.UpdateInterval)
        {
            return;
        }

        _sinceUpdate = 0;
        UpdateNavigation();
    }

    public override void Dispose()
    {
        _facts.FactChanged -= HandleFactChanged;
        base.Dispose();
    }

    /// <summary>The highest priority available map with a layer over the point; null when none.</summary>
    public MapDefinition? FindMapAt(Vector3 point, out int layerIndex)
    {
        ConditionContext context = MakeContext(Entity.Null);
        MapDefinition? best = null;
        layerIndex = -1;
        foreach (MapDefinition map in Maps)
        {
            if (best is not null && map.Priority <= best.Priority)
            {
                continue;
            }

            int layer = map.FindLayerAt(point);
            if (layer >= 0 && map.AvailableWhen.Evaluate(context))
            {
                best = map;
                layerIndex = layer;
            }
        }

        return best;
    }

    /// <summary>Marks a place with no entity, such as a quest area; the definition is a marker data asset.</summary>
    public MarkerHandle AddMarker(string definition, Vector3 location, string label = "")
    {
        MarkerDefinition? loaded = LoadDefinition(definition);
        if (loaded is null)
        {
            return MarkerHandle.None;
        }

        return Add(new Marker(NextHandle(), loaded) { Location = location, Label = label });
    }

    public bool RemoveMarker(MarkerHandle handle)
    {
        if (!_markers.Remove(handle.Id, out Marker? marker))
        {
            return false;
        }

        if (marker.Entity != Entity.Null)
        {
            _entityMarkers.Remove(marker.Entity);
        }

        if (Waypoint == handle)
        {
            Waypoint = MarkerHandle.None;
            _events.Send(new WaypointChangedEvent(Waypoint));
        }

        if (Tracked == handle)
        {
            SetTracked(MarkerHandle.None);
        }

        _events.Send(new MarkerRemovedEvent(handle));
        return true;
    }

    /// <summary>The marker on an entity; not valid when it has none.</summary>
    public MarkerHandle FindMarker(Entity entity) => _entityMarkers.GetValueOrDefault(entity, MarkerHandle.None);

    public bool Contains(MarkerHandle handle) => _markers.ContainsKey(handle.Id);

    public bool IsVisible(MarkerHandle handle) => _markers.TryGetValue(handle.Id, out Marker? marker) && marker.Visible;

    public bool IsDiscovered(MarkerHandle handle) => _markers.TryGetValue(handle.Id, out Marker? marker) && marker.Discovered;

    public MarkerDefinition? GetDefinition(MarkerHandle handle) => _markers.GetValueOrDefault(handle.Id)?.Definition;

    /// <summary>The entity of a marker on one; <see cref="Entity.Null"/> for a marker at a place.</summary>
    public Entity GetEntity(MarkerHandle handle)
    {
        if (_markers.TryGetValue(handle.Id, out Marker? marker))
        {
            return marker.Entity;
        }

        return Entity.Null;
    }

    public Vector3 GetLocation(MarkerHandle handle)
    {
        if (_markers.TryGetValue(handle.Id, out Marker? marker))
        {
            return GetLocation(marker);
        }

        return Vector3.Zero;
    }

    /// <summary>Sets the marker's discovered fact and applies its discovery effects; false when it was found already or can't be.</summary>
    public bool Discover(MarkerHandle handle, Entity discoverer)
    {
        if (!_markers.TryGetValue(handle.Id, out Marker? marker) || marker.Discovered)
        {
            return false;
        }

        DiscoveryFragment? discovery = marker.Definition.FindFragment<DiscoveryFragment>();
        if (discovery is null)
        {
            return false;
        }

        // Setting the fact and applying the effects can add or remove markers, so nothing below reads the dictionary.
        marker.Discovered = true;
        Entity instigator = discoverer;
        if (instigator == Entity.Null)
        {
            instigator = Navigator;
        }

        ConditionContext context = MakeContext(instigator, marker.Entity);
        if (marker.DiscoveredFact.IsValid)
        {
            _facts.SetFact(marker.DiscoveredFact, 1);
        }

        discovery.OnDiscovered.Apply(context);
        _events.Send(new MarkerDiscoveredEvent(handle));
        return true;
    }

    /// <summary>Checks every marker's conditions and discovery again; runs by itself whenever a fact changes.</summary>
    public void Refresh()
    {
        List<MarkerHandle> changed = [];
        foreach (Marker marker in _markers.Values.ToList())
        {
            if (UpdateMarker(marker))
            {
                changed.Add(marker.Handle);
            }
        }

        foreach (MarkerHandle handle in changed)
        {
            _events.Send(new MarkerVisibilityChangedEvent(handle));
        }
    }

    /// <summary>The marker the player follows: it shows at any distance and stays on the minimap's and compass's edges.</summary>
    public void SetTracked(MarkerHandle handle)
    {
        MarkerHandle tracked = MarkerHandle.None;
        if (Contains(handle))
        {
            tracked = handle;
        }

        if (Tracked == tracked)
        {
            return;
        }

        Tracked = tracked;
        _events.Send(new TrackedMarkerChangedEvent(Tracked));
    }

    /// <summary>Replaces the player's waypoint with one at the place and tracks it; not valid when the settings name no waypoint marker.</summary>
    public MarkerHandle SetWaypoint(Vector3 location)
    {
        MarkerHandle previous = Waypoint;
        Waypoint = MarkerHandle.None;
        RemoveMarker(previous);
        Waypoint = AddMarker(_settings.WaypointMarker, location);
        SetTracked(Waypoint);
        _events.Send(new WaypointChangedEvent(Waypoint));
        return Waypoint;
    }

    public void ClearWaypoint() => RemoveMarker(Waypoint);

    /// <summary>Checks the map underfoot and the places near the navigator now, rather than at its next check.</summary>
    public void UpdateNavigation()
    {
        if (!HasNavigator())
        {
            return;
        }

        UpdateActiveMap();
        GatherNearby();
    }

    public void SetMinimapRadius(float radius)
    {
        if (!HasNavigator())
        {
            return;
        }

        MinimapSettings minimap = World.Get<Navigator>(Navigator).Minimap;
        minimap.Radius = Math.Clamp(radius, minimap.MinRadius, minimap.MaxRadius);
    }

    /// <summary>Multiplies the minimap's radius; below 1 zooms in.</summary>
    public void ZoomMinimap(float factor)
    {
        if (HasNavigator())
        {
            float radius = World.Get<Navigator>(Navigator).Minimap.Radius;
            SetMinimapRadius(radius * factor);
        }
    }

    public void SetCategoryHidden(Tag category, bool hidden)
    {
        if (!HasNavigator())
        {
            return;
        }

        List<Tag> categories = World.Get<Navigator>(Navigator).HiddenCategories;
        categories.Remove(category);
        if (hidden)
        {
            categories.Add(category);
        }
    }

    /// <summary>The minimap's radius, or less in a room (see <see cref="MinimapSettings.RoomRadiusScale"/>).</summary>
    public float GetMinimapRadius()
    {
        if (!HasNavigator())
        {
            return 0;
        }

        MinimapSettings minimap = World.Get<Navigator>(Navigator).Minimap;
        MapLayer? layer = ActiveMap?.GetLayer(ActiveLayer);
        if (layer is null || !layer.LimitHeight || minimap.RoomRadiusScale <= 0)
        {
            return minimap.Radius;
        }

        Vector2 size = layer.Size;
        float room = 0.5f * MathF.Max(size.X, size.Y) * minimap.RoomRadiusScale;
        return MathF.Min(MathF.Max(room, minimap.MinRadius), minimap.Radius);
    }

    /// <summary>The minimap for a view heading; a radius of 0 uses <see cref="GetMinimapRadius"/>, and an eased one animates zooming.</summary>
    public MinimapView BuildMinimapView(float viewHeading, float radius = 0)
    {
        if (!HasNavigator())
        {
            return new MinimapView();
        }

        Navigator navigator = World.Get<Navigator>(Navigator);
        Vector3 center = GetNavigatorPosition();
        float mapYaw = 0;
        if (navigator.Minimap.RotateWithView)
        {
            mapYaw = viewHeading;
        }

        if (radius <= 0)
        {
            radius = GetMinimapRadius();
        }

        radius = MathF.Max(radius, 0.01f);
        float ownerHeading = GetHeading(Navigator);
        MinimapView view = new()
        {
            Map = ActiveMap,
            LayerIndex = ActiveLayer,
            BaseLayerIndex = ActiveLayer,
            Radius = radius,
            MapRotation = -mapYaw,
            PlayerRotation = ownerHeading - mapYaw,
        };
        FillLayers(view, center, radius);

        (float sin, float cos) = MathF.SinCos(float.DegreesToRadians(mapYaw));
        foreach (Marker marker in GetCandidates())
        {
            bool tracked = marker.Handle == Tracked;
            MinimapFragment? fragment = marker.Definition.FindFragment<MinimapFragment>();
            if (fragment is null || !PassesFilters(marker, tracked, navigator))
            {
                continue;
            }

            Vector3 delta = GetLocation(marker) - center;
            float ground = new Vector2(delta.X, delta.Z).Length();
            if (!tracked && fragment.MaxDistance > 0 && ground > fragment.MaxDistance)
            {
                continue;
            }

            float forward = (delta.X * sin) - (delta.Z * cos);
            float right = (delta.X * cos) + (delta.Z * sin);
            Vector2 position = new(right / radius, -forward / radius);
            bool clamped = false;
            if (position.LengthSquared() > 1)
            {
                if (!tracked && !fragment.ClampToEdge)
                {
                    continue;
                }

                position = Vector2.Normalize(position);
                clamped = true;
            }

            float rotation = 0;
            if (fragment.RotateWithEntity)
            {
                rotation = GetHeading(marker.Entity) - mapYaw;
            }

            MarkerView markerView = MakeView(marker, center, tracked) with { Position = position, Clamped = clamped, Rotation = rotation };
            view.Markers.Add(markerView);
        }

        SortByPriority(view.Markers);
        return view;
    }

    /// <summary>The compass for a view heading.</summary>
    public CompassView BuildCompassView(float viewHeading)
    {
        CompassView view = new() { Heading = Wrap(viewHeading) };
        if (!HasNavigator())
        {
            return view;
        }

        Navigator navigator = World.Get<Navigator>(Navigator);
        Vector3 center = GetNavigatorPosition();
        float half = navigator.Compass.FieldOfView * 0.5f;
        foreach (Marker marker in GetCandidates())
        {
            bool tracked = marker.Handle == Tracked;
            CompassFragment? fragment = marker.Definition.FindFragment<CompassFragment>();
            if (fragment is null || !PassesFilters(marker, tracked, navigator))
            {
                continue;
            }

            Vector3 delta = GetLocation(marker) - center;
            float ground = new Vector2(delta.X, delta.Z).Length();
            if (!tracked && fragment.MaxDistance > 0 && ground > fragment.MaxDistance)
            {
                continue;
            }

            float bearing = Bearing(delta);
            float relative = Normalize(bearing - viewHeading);
            bool clamped = false;
            if (MathF.Abs(relative) > half)
            {
                if (!tracked)
                {
                    continue;
                }

                relative = MathF.Sign(relative) * half;
                clamped = true;
            }

            Vector2 position = new(relative / half, 0);
            MarkerView markerView = MakeView(marker, center, tracked) with { Position = position, Rotation = relative, Clamped = clamped };
            view.Markers.Add(markerView);
        }

        SortByPriority(view.Markers);
        return view;
    }

    /// <summary>The world map of one layer; the zoom is the world map's own (1 shows the whole layer) and only hides markers below their minimum.</summary>
    public WorldMapView BuildWorldMapView(MapDefinition map, int layerIndex, float zoom = 1)
    {
        MapLayer? layer = map.GetLayer(layerIndex);
        if (layer is null || !HasNavigator())
        {
            return new WorldMapView { Map = map, LayerIndex = layerIndex };
        }

        Navigator navigator = World.Get<Navigator>(Navigator);
        Vector3 player = GetNavigatorPosition();
        WorldMapView view = new()
        {
            Map = map,
            LayerIndex = layerIndex,
            PlayerOnLayer = layer.Contains(player),
            PlayerUv = layer.WorldToUv(player),
            PlayerRotation = GetHeading(Navigator),
        };
        foreach (Marker marker in _markers.Values)
        {
            bool tracked = marker.Handle == Tracked;
            WorldMapFragment? fragment = marker.Definition.FindFragment<WorldMapFragment>();
            if (fragment is null || !PassesFilters(marker, tracked, navigator) || (!tracked && zoom < fragment.MinZoom))
            {
                continue;
            }

            Vector3 location = GetLocation(marker);
            if (!layer.Contains(location) && !(tracked && layer.ContainsGround(location)))
            {
                continue;
            }

            float rotation = 0;
            if (fragment.RotateWithEntity)
            {
                rotation = GetHeading(marker.Entity);
            }

            Vector2 uv = layer.WorldToUv(location);
            MarkerView markerView = MakeView(marker, player, tracked) with { Position = uv, Rotation = rotation };
            view.Markers.Add(markerView);
        }

        SortByPriority(view.Markers);
        return view;
    }

    private static float Bearing(Vector3 delta)
    {
        float radians = MathF.Atan2(delta.X, -delta.Z);
        return float.RadiansToDegrees(radians);
    }

    /// <summary>The same turn between -180 and 180 degrees.</summary>
    private static float Normalize(float degrees)
    {
        float turned = degrees % 360;
        if (turned > 180)
        {
            turned -= 360;
        }
        else if (turned <= -180)
        {
            turned += 360;
        }

        return turned;
    }

    private static float Wrap(float degrees)
    {
        float wrapped = degrees % 360;
        if (wrapped < 0)
        {
            wrapped += 360;
        }

        return wrapped;
    }

    private static void SortByPriority(List<MarkerView> markers)
    {
        List<MarkerView> sorted = [.. markers.OrderBy(view => view.Definition?.Priority ?? 0)];
        markers.Clear();
        markers.AddRange(sorted);
    }

    private void FillLayers(MinimapView view, Vector3 center, float radius)
    {
        MapLayer? layer = ActiveMap?.GetLayer(ActiveLayer);
        if (ActiveMap is null || layer is null)
        {
            return;
        }

        view.CenterUv = layer.WorldToUv(center);
        view.ExtentUv = layer.RadiusToExtentUv(radius);
        view.BaseCenterUv = view.CenterUv;
        view.BaseExtentUv = view.ExtentUv;
        if (!layer.LimitHeight)
        {
            return;
        }

        int baseIndex = ActiveMap.FindBaseLayerAt(center);
        MapLayer? baseLayer = ActiveMap.GetLayer(baseIndex);
        if (baseLayer is not null)
        {
            view.BaseLayerIndex = baseIndex;
            view.BaseCenterUv = baseLayer.WorldToUv(center);
            view.BaseExtentUv = baseLayer.RadiusToExtentUv(radius);
        }
    }

    private MarkerView MakeView(Marker marker, Vector3 from, bool tracked)
    {
        Vector3 location = GetLocation(marker);
        float height = location.Y - from.Y;
        MarkerElevation elevation = MarkerElevation.Level;
        if (height > _settings.ElevationThreshold)
        {
            elevation = MarkerElevation.Above;
        }
        else if (height < -_settings.ElevationThreshold)
        {
            elevation = MarkerElevation.Below;
        }

        return new MarkerView
        {
            Handle = marker.Handle,
            Definition = marker.Definition,
            Label = GetLabel(marker),
            Distance = Vector3.Distance(from, location),
            Elevation = elevation,
            Discovered = marker.Discovered,
            Tracked = tracked,
        };
    }

    private bool PassesFilters(Marker marker, bool tracked, in Navigator navigator)
    {
        if (!marker.Visible)
        {
            return false;
        }

        if (!tracked && IsHidden(marker.Definition, navigator.HiddenCategories))
        {
            return false;
        }

        DiscoveryFragment? discovery = marker.Definition.FindFragment<DiscoveryFragment>();
        return marker.Discovered || discovery is null || !discovery.HiddenUntilDiscovered;
    }

    private static bool IsHidden(MarkerDefinition definition, List<Tag> hidden)
    {
        foreach (Tag tag in definition.Tags)
        {
            foreach (Tag category in hidden)
            {
                if (tag.MatchesTag(category))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The markers near the navigator at its last check, and the tracked one wherever it is.</summary>
    private List<Marker> GetCandidates()
    {
        List<Marker> candidates = [];
        foreach (MarkerHandle handle in _nearby)
        {
            if (_markers.TryGetValue(handle.Id, out Marker? marker))
            {
                candidates.Add(marker);
            }
        }

        if (_markers.TryGetValue(Tracked.Id, out Marker? followed) && !_nearby.Contains(Tracked))
        {
            candidates.Add(followed);
        }

        return candidates;
    }

    private void UpdateActiveMap()
    {
        Vector3 position = GetNavigatorPosition();
        MapDefinition? map = FindMapAt(position, out int layer);
        if (map == ActiveMap && layer == ActiveLayer)
        {
            return;
        }

        ActiveMap = map;
        ActiveLayer = layer;
        _events.Send(new ActiveMapChangedEvent(Navigator, map, layer));
    }

    /// <summary>One pass over every marker: discovers those in reach and keeps those the minimap or compass could show.</summary>
    private void GatherNearby()
    {
        _nearby.Clear();
        Navigator navigator = World.Get<Navigator>(Navigator);
        Vector3 position = GetNavigatorPosition();
        List<MarkerHandle> found = [];
        foreach (Marker marker in _markers.Values)
        {
            if (!marker.Visible)
            {
                continue;
            }

            Vector3 location = GetLocation(marker);
            DiscoveryFragment? discovery = marker.Definition.FindFragment<DiscoveryFragment>();
            if (!marker.Discovered && discovery is not null && Vector3.Distance(position, location) <= discovery.Radius)
            {
                found.Add(marker.Handle);
            }

            Vector3 delta = location - position;
            float ground = new Vector2(delta.X, delta.Z).Length();
            if (IsNearbyCandidate(marker, ground, navigator.Minimap))
            {
                _nearby.Add(marker.Handle);
            }
        }

        foreach (MarkerHandle handle in found)
        {
            Discover(handle, Navigator);
        }
    }

    private static bool IsNearbyCandidate(Marker marker, float ground, MinimapSettings minimap)
    {
        MinimapFragment? onMinimap = marker.Definition.FindFragment<MinimapFragment>();
        if (onMinimap is not null)
        {
            // Pinned markers sit on the edge however far away; the others must be inside the largest minimap.
            float range = MathF.Max(minimap.MaxRadius, minimap.Radius);
            if (onMinimap.ClampToEdge)
            {
                range = onMinimap.MaxDistance;
            }
            else if (onMinimap.MaxDistance > 0)
            {
                range = MathF.Min(range, onMinimap.MaxDistance);
            }

            if (range <= 0 || ground <= range + NearbyMargin)
            {
                return true;
            }
        }

        CompassFragment? onCompass = marker.Definition.FindFragment<CompassFragment>();
        return onCompass is not null && (onCompass.MaxDistance <= 0 || ground <= onCompass.MaxDistance + NearbyMargin);
    }

    private MarkerHandle Add(Marker marker)
    {
        marker.Discovered = marker.Definition.FindFragment<DiscoveryFragment>() is null;
        UpdateMarker(marker);
        _markers[marker.Handle.Id] = marker;
        if (marker.Entity != Entity.Null)
        {
            _entityMarkers[marker.Entity] = marker.Handle;
        }

        _events.Send(new MarkerAddedEvent(marker.Handle));
        return marker.Handle;
    }

    /// <summary>Reads discovery and visibility again; true when visibility changed.</summary>
    private bool UpdateMarker(Marker marker)
    {
        marker.Discovered = ReadDiscovered(marker);
        bool wasVisible = marker.Visible;
        ConditionContext context = MakeContext(Navigator, marker.Entity);
        marker.Visible = marker.Definition.VisibleWhen.Evaluate(context);
        return wasVisible != marker.Visible;
    }

    private SavedMarker? SaveMarker(Entity entity)
    {
        if (!_entityMarkers.TryGetValue(entity, out MarkerHandle handle) || !_markers.TryGetValue(handle.Id, out Marker? marker))
        {
            return null;
        }

        return new SavedMarker { Discovered = marker.Discovered };
    }

    private void RestoreMarker(Entity entity, SavedMarker saved)
    {
        if (!_entityMarkers.TryGetValue(entity, out MarkerHandle handle) || !_markers.TryGetValue(handle.Id, out Marker? marker))
        {
            return;
        }

        // A marker with a discovered fact follows the fact, which the save brought back already.
        if (!marker.DiscoveredFact.IsValid)
        {
            marker.Discovered = saved.Discovered;
        }
    }

    private SavedNavigation SaveNavigation()
    {
        SavedNavigation saved = new() { TrackingWaypoint = Tracked.IsValid && Tracked == Waypoint };
        if (Waypoint.IsValid)
        {
            saved.Waypoint = GetLocation(Waypoint);
        }

        Entity tracked = GetEntity(Tracked);
        if (tracked != Entity.Null && World.IsAlive(tracked) && World.TryGet(tracked, out Saveable saveable))
        {
            saved.Tracked = saveable.Id;
        }

        return saved;
    }

    private void RestoreNavigation(SavedNavigation saved)
    {
        ClearWaypoint();
        Waypoint = MarkerHandle.None;
        SetTracked(MarkerHandle.None);
        if (saved.Waypoint is { } waypoint)
        {
            SetWaypoint(waypoint);
            if (!saved.TrackingWaypoint)
            {
                SetTracked(MarkerHandle.None);
            }
        }

        if (saved.Tracked.Length == 0)
        {
            return;
        }

        foreach ((Entity entity, MarkerHandle handle) in _entityMarkers)
        {
            if (World.IsAlive(entity) && World.TryGet(entity, out Saveable saveable) && saveable.Id == saved.Tracked)
            {
                SetTracked(handle);
                return;
            }
        }
    }

    private bool ReadDiscovered(Marker marker)
    {
        if (marker.Definition.FindFragment<DiscoveryFragment>() is null)
        {
            return true;
        }

        if (!marker.DiscoveredFact.IsValid)
        {
            return marker.Discovered;
        }

        return _facts.GetFact(marker.DiscoveredFact) > 0;
    }

    private ConditionContext MakeContext(Entity instigator, Entity target = default)
    {
        IEntityView? instigatorView = ViewOf(instigator);
        IEntityView? targetView = ViewOf(target);
        return new ConditionContext(_facts) { Instigator = instigatorView, Target = targetView, Services = _services };
    }

    private IEntityView? ViewOf(Entity entity)
    {
        if (entity == Entity.Null || !World.IsAlive(entity))
        {
            return null;
        }

        return new WorldEntityView(World, entity);
    }

    private string GetLabel(Marker marker)
    {
        if (!string.IsNullOrEmpty(marker.Label))
        {
            return marker.Label;
        }

        return marker.Definition.Display.Name;
    }

    private Vector3 GetLocation(Marker marker)
    {
        if (marker.Entity != Entity.Null && World.IsAlive(marker.Entity) && World.TryGet(marker.Entity, out Transform transform))
        {
            return transform.Position;
        }

        return marker.Location;
    }

    /// <summary>Degrees clockwise from north that an entity faces down its +Z axis; 0 without a transform.</summary>
    private float GetHeading(Entity entity)
    {
        if (entity == Entity.Null || !World.IsAlive(entity) || !World.TryGet(entity, out Transform transform))
        {
            return 0;
        }

        Vector3 forward = Vector3.Transform(Vector3.UnitZ, transform.Rotation);
        return Bearing(forward);
    }

    private Vector3 GetNavigatorPosition()
    {
        if (World.TryGet(Navigator, out Transform transform))
        {
            return transform.Position;
        }

        return Vector3.Zero;
    }

    private bool HasNavigator() => Navigator != Entity.Null && World.IsAlive(Navigator) && World.Has<Navigator>(Navigator);

    private MarkerHandle NextHandle()
    {
        MarkerHandle handle = new(_nextId);
        _nextId++;
        return handle;
    }

    private MarkerDefinition? LoadDefinition(string path)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return data.Load<MarkerDefinition>(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }
    }

    private void Register(Entity entity, in MapMarker component)
    {
        if (!component.Enabled || _entityMarkers.ContainsKey(entity))
        {
            return;
        }

        MarkerDefinition? definition = LoadDefinition(component.Definition);
        if (definition is null)
        {
            return;
        }

        Marker marker = new(NextHandle(), definition) { Entity = entity, Label = component.Label, DiscoveredFact = component.DiscoveredFact };
        Add(marker);
    }

    private void Unregister(Entity entity)
    {
        if (_entityMarkers.TryGetValue(entity, out MarkerHandle handle))
        {
            RemoveMarker(handle);
        }
    }

    private void SetNavigator(Entity entity)
    {
        Navigator = entity;
        Refresh();
        UpdateNavigation();
    }

    private void ClearNavigator(Entity entity)
    {
        if (Navigator == entity)
        {
            Navigator = Entity.Null;
            _nearby.Clear();
        }
    }

    private void HandleFactChanged(Tag tag, int oldValue, int newValue) => Refresh();
}
