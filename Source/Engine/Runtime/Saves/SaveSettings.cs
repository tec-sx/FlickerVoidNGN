namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>Where saves are written and the slot checkpoints use.</summary>
public sealed class SaveSettings
{
    public string Folder { get; set; } = "Saves";

    public string CheckpointSlot { get; set; } = "Checkpoint";
}
