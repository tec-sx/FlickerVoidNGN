using Arch.Core;

namespace FlickerVoid.Engine.Interaction.Toggles;

/// <summary>Sent when a lock keeps a toggle from opening or closing, for a rattle or a line from the protagonist.</summary>
public readonly record struct ToggleBlockedEvent(Entity Target, Entity Instigator, bool Opening);
