namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>Sent when the player is to choose; answer with <see cref="DialogueSystem.Choose"/>.</summary>
public readonly record struct ChoicesPresentedEvent(IReadOnlyList<DialogueChoice> Choices);
