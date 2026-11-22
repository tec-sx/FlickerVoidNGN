using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>Shows the entity in scan mode and reveals its scan definition when scanned.</summary>
public struct Scannable()
{
    [ContentFile(".fvdata")]
    public string Definition { get; set; } = string.Empty;

    /// <summary>Metres above the entity's position that the scanner looks at.</summary>
    public float Height { get; set; } = 0.5f;
}
