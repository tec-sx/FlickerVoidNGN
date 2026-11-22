namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>A scannable while the game plays: its loaded definition, whether scan mode shows it and whether it was scanned.</summary>
public struct ActiveScannable
{
    public ScanDefinition Definition { get; set; }

    public bool Revealed { get; set; }

    public bool Scanned { get; set; }
}
