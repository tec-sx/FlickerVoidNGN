using Arch.Core;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>Sent when a scanner completes scanning a scannable.</summary>
public readonly record struct ScannedEvent(Entity Scanner, Entity Target);
