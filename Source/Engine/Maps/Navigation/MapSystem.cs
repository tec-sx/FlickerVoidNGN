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
using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>
/// The maps and markers of the world (a fixed system and service, ported from FVNavigationSystem): markers on entities and at places,
/// their visibility on fact changes, discovery, the tracked marker and the player's waypoint; and the navigator's map underfoot, the
/// places it discovers on its checks, and the minimap, compass and world map views. Headings are degrees clockwise from north, -Z.
/// </summary>
public sealed partial class MapSystem : BaseSystem<World, GameTime>
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
