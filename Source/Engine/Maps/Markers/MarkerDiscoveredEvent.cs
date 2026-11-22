namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>The navigator discovered a marker's place.</summary>
public readonly record struct MarkerDiscoveredEvent(MarkerHandle Marker);
