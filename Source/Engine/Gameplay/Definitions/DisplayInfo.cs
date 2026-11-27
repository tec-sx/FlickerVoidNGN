using FlickerVoid.Engine.Gameplay.Localization;

namespace FlickerVoid.Engine.Gameplay.Definitions;

/// <summary>The texts the player sees for a definition.</summary>
public sealed class DisplayInfo
{
    public LocalizedText Name { get; set; } = new();

    public LocalizedText ShortDescription { get; set; } = new();

    public LocalizedText LongDescription { get; set; } = new();
}
