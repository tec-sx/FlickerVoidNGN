using Arch.Core;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>Sent when a scanner focuses another scannable, or none (<see cref="Entity.Null"/>).</summary>
public readonly record struct ScanFocusChangedEvent(Entity Scanner, Entity Target);
