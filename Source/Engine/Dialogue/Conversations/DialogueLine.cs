using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>A line being spoken, for the interface and the speaker's animation.</summary>
public sealed record DialogueLine
{
    public static DialogueLine None { get; } = new();

    /// <summary>The speaker's character id; none means the conversation's owner.</summary>
    public Tag Speaker { get; init; }

    /// <summary>The entity that speaks it; <see cref="Entity.Null"/> when no participant is that speaker.</summary>
    public Entity SpeakerEntity { get; init; } = Entity.Null;

    public string Text { get; init; } = "";

    public Tag Mood { get; init; }

    /// <summary>How long the line shows, in seconds, before it finishes by itself.</summary>
    public float Duration { get; init; }

    public bool Skippable { get; init; } = true;

    /// <summary>The line waits for <see cref="DialogueSystem.Advance"/> instead of finishing after its duration.</summary>
    public bool WaitForInput { get; init; }
}
