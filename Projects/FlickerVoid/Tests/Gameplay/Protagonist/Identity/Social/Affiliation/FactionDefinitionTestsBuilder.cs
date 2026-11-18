using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Affiliation;

/// <summary>Builds the faction under test and its standing scale.</summary>
internal sealed class FactionDefinitionTestsBuilder
{
    private readonly List<ScaleBand> _bands = [];
    private readonly List<ScaleLink> _links = [];
    private readonly Dictionary<Tag, Attitude> _attitudes = [];
    private Tag _standingFact;

    public FactionDefinitionTestsBuilder WithStandingFact(Tag fact)
    {
        _standingFact = fact;
        return this;
    }

    public FactionDefinitionTestsBuilder WithBand(int threshold, Tag tag)
    {
        ScaleBand band = new() { Threshold = threshold, Tag = tag };
        _bands.Add(band);
        return this;
    }

    public FactionDefinitionTestsBuilder WithAttitude(Tag band, Attitude attitude)
    {
        _attitudes[band] = attitude;
        return this;
    }

    public FactionDefinitionTestsBuilder WithLink(ScaleDefinition scale, float factor, bool magnitude = false)
    {
        ScaleLink link = new() { Scale = scale, Factor = factor, Magnitude = magnitude };
        _links.Add(link);
        return this;
    }

    public FactionDefinition Build()
    {
        ScaleDefinition standing = new() { Id = _standingFact, Bands = [.. _bands], Links = [.. _links] };
        return new FactionDefinition { Standing = standing, BandAttitudes = new(_attitudes) };
    }
}
