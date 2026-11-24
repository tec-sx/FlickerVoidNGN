using Arch.Core;

using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Runtime.Saves;

namespace FlickerVoid.Engine.Maps.Navigation;

public sealed partial class MapSystem
{
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
}
