using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Dialogue.Barks;

/// <summary>One ambient line of a bark table, said only by speakers its tags and conditions fit.</summary>
public sealed class BarkRow
{
    public string Text { get; set; } = "";

    /// <summary>Seconds the line shows.</summary>
    public float DisplayDuration { get; set; } = 3;

    /// <summary>A tag the speaker must own; none means any speaker.</summary>
    public Tag IdentityTag { get; set; }

    /// <summary>Tags the speaker and the other side of the flow must own between them.</summary>
    public List<Tag> RequiredTags { get; set; } = [];

    /// <summary>Tags neither of them may own.</summary>
    public List<Tag> BlockingTags { get; set; } = [];

    public ConditionSet Conditions { get; set; } = new();
}
