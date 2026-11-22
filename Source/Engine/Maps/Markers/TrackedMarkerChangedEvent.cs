namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>The marker the player follows changed; none when the handle is not valid.</summary>
public readonly record struct TrackedMarkerChangedEvent(MarkerHandle Marker);
