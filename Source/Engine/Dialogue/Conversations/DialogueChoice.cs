using FlickerVoid.Engine.Gameplay.Localization;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>An option shown to the player; <see cref="Index"/> is what <see cref="DialogueSystem.Choose"/> takes.</summary>
public readonly record struct DialogueChoice(int Index, LocalizedText Text, bool Available, LocalizedText LockedReason);
