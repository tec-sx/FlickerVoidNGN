using Arch.Core;

namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>Sent when an activator leaves a trigger area, or the area or the activator is gone.</summary>
public readonly record struct AreaExitedEvent(Entity Area, Entity Activator);
