namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>An option shown to the player; <see cref="Index"/> is what <see cref="DialogueSystem.Choose"/> takes.</summary>
public readonly record struct DialogueChoice(int Index, string Text, bool Available, string LockedReason);
