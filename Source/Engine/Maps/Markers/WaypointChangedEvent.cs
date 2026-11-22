namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>The player's waypoint was set or cleared.</summary>
public readonly record struct WaypointChangedEvent(MarkerHandle Marker);
