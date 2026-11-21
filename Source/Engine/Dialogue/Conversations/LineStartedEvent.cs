namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>Sent when a line starts; the speaker's entity is in the line.</summary>
public readonly record struct LineStartedEvent(DialogueLine Line);
