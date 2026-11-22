using Arch.Core;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>Sent when a scanner turns scan mode on or off.</summary>
public readonly record struct ScanModeChangedEvent(Entity Scanner, bool Active);
