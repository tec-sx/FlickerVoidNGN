namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>How long lines without their own duration show: the longer of the minimum and the text's length times the time per character.</summary>
public sealed class DialogueSettings
{
    public float MinLineDuration { get; set; } = 1.5f;

    public float SecondsPerCharacter { get; set; } = 0.06f;
}
