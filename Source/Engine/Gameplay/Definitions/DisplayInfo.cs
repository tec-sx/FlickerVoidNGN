namespace FlickerVoid.Engine.Gameplay.Definitions;

/// <summary>The texts the player sees for a definition.</summary>
public sealed class DisplayInfo
{
    public string Name { get; init; } = string.Empty;

    public string ShortDescription { get; init; } = string.Empty;

    public string LongDescription { get; init; } = string.Empty;
}
