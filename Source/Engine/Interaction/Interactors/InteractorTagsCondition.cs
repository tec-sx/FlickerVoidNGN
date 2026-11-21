using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>The instigating interactor's tags (its <see cref="OwnedTags"/>) include the required ones and none of the blocked ones.</summary>
public sealed class InteractorTagsCondition : Condition
{
    public List<Tag> Required { get; set; } = [];

    /// <summary>Every required tag, rather than any one.</summary>
    public bool RequireAll { get; set; }

    public List<Tag> Blocked { get; set; } = [];

    public override string Describe()
    {
        List<string> parts = [];
        if (Required.Count > 0)
        {
            string which = RequireAll ? "all" : "any";
            parts.Add($"Requires {which} of {string.Join(", ", Required)}");
        }

        if (Blocked.Count > 0)
        {
            parts.Add($"Blocked by {string.Join(", ", Blocked)}");
        }

        return string.Join(", ", parts);
    }

    protected override bool Evaluate(ConditionContext context)
    {
        IEntityView? instigator = context.Instigator;
        if (instigator is null || !instigator.TryGet(out OwnedTags owned))
        {
            return Required.Count == 0;
        }

        foreach (Tag blocked in Blocked)
        {
            if (owned.HasMatchingTag(blocked))
            {
                return false;
            }
        }

        if (Required.Count == 0)
        {
            return true;
        }

        if (RequireAll)
        {
            return Required.TrueForAll(owned.HasMatchingTag);
        }

        return Required.Exists(owned.HasMatchingTag);
    }
}
