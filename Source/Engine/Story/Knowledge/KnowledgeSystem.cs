using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Story.Knowledge;

/// <summary>What the protagonist knows: clues, topics, thoughts and documents, each kept in the fact its id names.</summary>
public sealed class KnowledgeSystem(IFactDatabase facts, IServiceRegistry services)
{
    /// <summary>Who learning applies its effects for, as their instigator: the player, which the game sets.</summary>
    public IEntityView? Player { get; set; }

    public bool Knows(Tag knowledge) => facts.GetFact(knowledge) > 0;

    /// <summary>True when the knowledge has a file, isn't known yet and its prerequisites are.</summary>
    public bool CanLearn(Tag knowledge)
    {
        KnowledgeDefinition? definition = Find(knowledge);
        return definition is not null && CanLearn(definition);
    }

    /// <summary>Sets the knowledge's fact and applies its effects; false when it is known or its prerequisites aren't.</summary>
    public bool Learn(Tag knowledge)
    {
        KnowledgeDefinition? definition = Find(knowledge);
        if (definition is null || !CanLearn(definition))
        {
            return false;
        }

        facts.SetFact(knowledge, 1);
        ConditionContext context = new(facts) { Services = services, Instigator = Player };
        definition.OnLearned.Apply(context);
        return true;
    }

    public void Forget(Tag knowledge) => facts.RemoveFact(knowledge);

    /// <summary>Every known knowledge of the content folder of the given kinds, or of every kind when none are given.</summary>
    public IReadOnlyList<KnowledgeDefinition> GetKnown(params KnowledgeKind[] kinds)
    {
        List<KnowledgeDefinition> known = [];
        DataAssets? data = services.GetService<DataAssets>();
        if (data is null)
        {
            return known;
        }

        foreach (KnowledgeDefinition definition in data.LoadAll<KnowledgeDefinition>())
        {
            bool ofKind = kinds.Length == 0 || kinds.Contains(definition.Kind);
            if (ofKind && Knows(definition.Id))
            {
                known.Add(definition);
            }
        }

        return known;
    }

    private bool CanLearn(KnowledgeDefinition definition)
    {
        if (Knows(definition.Id))
        {
            return false;
        }

        foreach (Tag prerequisite in definition.Prerequisites)
        {
            if (!Knows(prerequisite))
            {
                return false;
            }
        }

        return true;
    }

    private KnowledgeDefinition? Find(Tag knowledge)
    {
        DataAssets? data = services.GetService<DataAssets>();
        return data?.Find<KnowledgeDefinition>(knowledge);
    }
}
