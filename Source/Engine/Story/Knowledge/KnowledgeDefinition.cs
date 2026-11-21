using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Knowledge;

/// <summary>A piece of knowledge; its id is the fact set to 1 when it is learned (Knowledge.Clue.BloodOnCoat).</summary>
public sealed class KnowledgeDefinition : Definition
{
    public KnowledgeKind Kind { get; set; } = KnowledgeKind.Clue;

    /// <summary>The ids of the knowledge that must be known before this can be learned.</summary>
    public List<Tag> Prerequisites { get; set; } = [];

    public EffectList OnLearned { get; set; } = new();
}
