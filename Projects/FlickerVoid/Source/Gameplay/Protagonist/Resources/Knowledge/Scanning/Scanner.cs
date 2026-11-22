namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>Lets its entity scan: in scan mode it reveals the scannables within range and scans the one the pointer is on while scan is held.</summary>
public struct Scanner()
{
    public float Range { get; set; } = 8;

    /// <summary>Metres above the entity's position that it looks from.</summary>
    public float EyeHeight { get; set; } = 1.5f;

    public bool RequireLineOfSight { get; set; } = true;

    /// <summary>Seconds between updates of what is revealed and focused.</summary>
    public float UpdateInterval { get; set; } = 0.1f;
}
