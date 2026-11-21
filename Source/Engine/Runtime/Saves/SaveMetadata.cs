namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>What a save slot shows before it is loaded.</summary>
public sealed class SaveMetadata
{
    /// <summary>The <see cref="SaveGame.CurrentVersion"/> the save was written with; newer saves don't load.</summary>
    public int Version { get; set; }

    public string Slot { get; set; } = "";

    public DateTimeOffset Timestamp { get; set; }

    public TimeSpan PlayTime { get; set; }

    public bool IsCheckpoint { get; set; }
}
