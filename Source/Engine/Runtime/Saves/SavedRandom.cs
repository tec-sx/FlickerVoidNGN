namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>Where the game's random source was when it was saved.</summary>
public sealed class SavedRandom
{
    public ulong State { get; set; }
}
