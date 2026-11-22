using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>Makes an entity a speaker in dialogue and, with a dialogue graph, someone to talk to.</summary>
public struct DialogueParticipant
{
    public DialogueParticipant()
    {
    }

    /// <summary>The character's id, which lines name as their speaker.</summary>
    public Tag Speaker { get; set; }

    /// <summary>The conversation's flow graph; empty for an entity that only speaks in others' conversations. A rule may swap it.</summary>
    [ContentFile(".fvflow")]
    public string Dialogue { get; set; } = string.Empty;
}
