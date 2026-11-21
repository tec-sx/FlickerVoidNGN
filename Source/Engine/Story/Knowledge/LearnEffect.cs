using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Knowledge;

/// <summary>Learns a piece of knowledge through the <see cref="KnowledgeSystem"/>.</summary>
public sealed class LearnEffect : Effect
{
    public Tag Knowledge { get; set; }

    public override void Apply(ConditionContext context)
    {
        KnowledgeSystem? knowledge = context.GetService<KnowledgeSystem>();
        knowledge?.Learn(Knowledge);
    }

    public override string Describe() => $"Learn {Knowledge}";
}
