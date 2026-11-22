using Arch.Core;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>A scanner while the game plays: scan mode, the scannables it reveals, its focus and the progress of scanning it.</summary>
public struct ScannerState
{
    public bool Active { get; set; }

    public bool Scanning { get; set; }

    public Entity Focus { get; set; }

    /// <summary>From 0 to 1.</summary>
    public float Progress { get; set; }

    public float UpdateLeft { get; set; }

    public List<Entity> Revealed { get; set; }
}
