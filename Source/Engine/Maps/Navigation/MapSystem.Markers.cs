using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Maps.Navigation;

public sealed partial class MapSystem
{
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

    private MarkerHandle NextHandle()
    {
        MarkerHandle handle = new(_nextId);
        _nextId++;
        return handle;
    }
}
