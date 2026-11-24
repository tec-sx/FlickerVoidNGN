using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Tests.Quests;

/// <summary>Sets its fact to the instigator's <see cref="Mark"/>, so tests see whom a flow applied its effects for.</summary>
internal sealed class MarkEffect : Effect
{
    public Tag Fact { get; set; }

    public override void Apply(ConditionContext context)
    {
        if (context.Instigator is not null && context.Instigator.TryGet(out Mark mark))
        {
            context.Facts.SetFact(Fact, mark.Value);
        }
    }
}
