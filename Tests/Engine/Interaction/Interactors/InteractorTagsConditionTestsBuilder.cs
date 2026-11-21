using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactors;

namespace FlickerVoid.Engine.Interaction.Tests.Interactors;

/// <summary>Builds the condition under test and a context whose instigator owns the given tags, or none.</summary>
internal sealed class InteractorTagsConditionTestsBuilder
{
    private readonly InteractorTagsCondition _condition = new();
    private Tag[]? _owned;

    public InteractorTagsConditionTestsBuilder Requiring(bool all, params string[] tags)
    {
        _condition.RequireAll = all;
        _condition.Required = tags.Select(static tag => new Tag(tag)).ToList();
        return this;
    }

    public InteractorTagsConditionTestsBuilder Blocking(params string[] tags)
    {
        _condition.Blocked = tags.Select(static tag => new Tag(tag)).ToList();
        return this;
    }

    public InteractorTagsConditionTestsBuilder WithOwned(params string[] tags)
    {
        _owned = tags.Select(static tag => new Tag(tag)).ToArray();
        return this;
    }

    public bool Check()
    {
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        IEntityView instigator = Substitute.For<IEntityView>();
        if (_owned is not null)
        {
            OwnedTags owned = new(_owned);
            instigator.TryGet(out Arg.Any<OwnedTags>()).Returns(call =>
            {
                call[0] = owned;
                return true;
            });
        }

        ConditionContext context = new(facts) { Instigator = instigator };
        return _condition.Check(context);
    }
}
