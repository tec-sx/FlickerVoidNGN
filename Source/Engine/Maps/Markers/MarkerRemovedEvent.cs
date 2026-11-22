namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>A marker was removed.</summary>
public readonly record struct MarkerRemovedEvent(MarkerHandle Marker);
