namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>One saved game: its metadata, the facts and what modules write into it.</summary>
public sealed class SaveGame
{
    public const int CurrentVersion = 1;

    public SaveMetadata Metadata { get; set; } = new();

    /// <summary>Every fact by its tag's name.</summary>
    public Dictionary<string, int> Facts { get; set; } = [];

    /// <summary>Values modules keep by their own keys.</summary>
    public Dictionary<string, string> CustomData { get; set; } = [];
}
