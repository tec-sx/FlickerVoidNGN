using Arch.Core;

namespace FlickerVoid.Engine.Interaction.Toggles;

/// <summary>Sent when a toggle opens or closes, with who did it; presentation moves the door or lid.</summary>
public readonly record struct ToggledEvent(Entity Target, bool Open, Entity Instigator);
