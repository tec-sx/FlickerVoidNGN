using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Fame;

/// <summary>Builds the titles under test, tracking the given facts.</summary>
internal sealed class TitlesTestsBuilder(IFactDatabase facts)
{
    private readonly List<TitleDefinition> _titles = [];

    public TitlesTestsBuilder WithTitles(params TitleDefinition[] titles)
    {
        _titles.AddRange(titles);
        return this;
    }

    public Titles Build()
    {
        SocialSettings settings = new() { Titles = [.. _titles] };
        return new Titles(facts, settings);
    }
}
