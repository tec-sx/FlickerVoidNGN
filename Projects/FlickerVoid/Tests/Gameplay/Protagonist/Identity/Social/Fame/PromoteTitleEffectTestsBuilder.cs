using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Fame;

/// <summary>Builds the promote-title effect under test for a title, and a context whose services hold substitute titles.</summary>
internal sealed class PromoteTitleEffectTestsBuilder
{
    private readonly TitleDefinition _title;

    public PromoteTitleEffectTestsBuilder(TitleDefinition title)
    {
        _title = title;
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        Context = SocialContext.Create(facts, (typeof(ITitles), Titles));
    }

    public ITitles Titles { get; } = Substitute.For<ITitles>();

    public ConditionContext Context { get; }

    public PromoteTitleEffect Build() => new() { Title = _title };
}
