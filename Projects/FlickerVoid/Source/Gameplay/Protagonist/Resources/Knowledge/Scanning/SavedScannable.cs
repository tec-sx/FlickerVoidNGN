namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>What a save keeps of a scannable: whether it was scanned, so its effects apply only once.</summary>
public sealed class SavedScannable
{
    public bool Scanned { get; set; }
}
