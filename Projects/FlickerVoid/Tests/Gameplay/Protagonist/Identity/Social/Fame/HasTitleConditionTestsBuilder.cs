using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Fame;

/// <summary>Builds the has-title condition under test for the given title.</summary>
internal sealed class HasTitleConditionTestsBuilder(TitleDefinition title)
{
    public HasTitleCondition Build() => new() { Title = title };
}
