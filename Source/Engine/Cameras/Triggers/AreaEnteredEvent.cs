using Arch.Core;

namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>Sent when an activator comes into a trigger area.</summary>
public readonly record struct AreaEnteredEvent(Entity Area, Entity Activator);
