using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests.Scales;

/// <summary>Builds the scale under test.</summary>
internal sealed class ScaleDefinitionTestsBuilder
{
    private readonly List<ScaleBand> _bands = [];
    private readonly List<ScaleLink> _links = [];
    private Tag _fact;
    private string _name = string.Empty;

    /// <summary>The fact that keeps the scale's value.</summary>
    public ScaleDefinitionTestsBuilder WithFact(Tag fact)
    {
        _fact = fact;
        return this;
    }

    public ScaleDefinitionTestsBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ScaleDefinitionTestsBuilder WithBand(ScaleBand band)
    {
        _bands.Add(band);
        return this;
    }

    public ScaleDefinitionTestsBuilder WithLink(ScaleDefinition scale, float factor, bool magnitude = false)
    {
        ScaleLink link = new() { Scale = scale, Factor = factor, Magnitude = magnitude };
        _links.Add(link);
        return this;
    }

    public ScaleDefinition Build()
    {
        DisplayInfo display = new() { Name = new LocalizedText("", _name) };
        return new ScaleDefinition { Id = _fact, Display = display, Bands = [.. _bands], Links = [.. _links] };
    }
}
