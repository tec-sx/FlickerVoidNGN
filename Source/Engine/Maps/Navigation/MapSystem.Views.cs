using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Maps.Definitions;
using FlickerVoid.Engine.Maps.Markers;

namespace FlickerVoid.Engine.Maps.Navigation;

public sealed partial class MapSystem
{
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
}
