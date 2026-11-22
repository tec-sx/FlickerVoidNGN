namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>A marker's conditions started or stopped holding.</summary>
public readonly record struct MarkerVisibilityChangedEvent(MarkerHandle Marker);
