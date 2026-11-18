using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

/// <summary>A name the world gives the player once its conditions hold; earned titles are kept for good, and Id is the fact holding the title's rank (0 = not earned).</summary>
public sealed class TitleDefinition : Definition
{
    /// <summary>Checked whenever a fact changes; their at-least thresholds rank the title, and the hardest held title is current.</summary>
    public ConditionSet Conditions { get; init; } = new();

    /// <summary>Applied once, the first time the title is earned.</summary>
    public EffectList OnEarned { get; init; } = new();

    public bool IsEarned(IFactDatabase facts) => GetRank(facts) > 0;

    /// <summary>The title's place in the player's history: the highest rank is the current title, 0 means not earned.</summary>
    public int GetRank(IFactDatabase facts)
    {
        if (!Id.IsValid)
        {
            return 0;
        }

        int rank = facts.GetFact(Id);
        return Math.Max(0, rank);
    }
}
