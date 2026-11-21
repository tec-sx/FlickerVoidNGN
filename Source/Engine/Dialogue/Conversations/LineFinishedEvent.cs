namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>Sent when a line finishes, by itself or by <see cref="DialogueSystem.Advance"/>.</summary>
public readonly record struct LineFinishedEvent(DialogueLine Line);
