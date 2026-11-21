using Arch.Core;

namespace FlickerVoid.Engine.Interaction.Locks;

/// <summary>Sent when a lock locks or unlocks, with who did it.</summary>
public readonly record struct LockChangedEvent(Entity Target, bool Locked, Entity Instigator);
