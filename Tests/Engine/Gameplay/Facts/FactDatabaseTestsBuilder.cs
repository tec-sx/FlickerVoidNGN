using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests.Facts;

/// <summary>Builds the fact database under test from fact definitions and starting values.</summary>
internal sealed class FactDatabaseTestsBuilder
{
    private readonly List<FactDefinition> _definitions = [];
    private readonly Dictionary<Tag, int> _facts = [];

    public FactDatabaseTestsBuilder WithDefinition(FactDefinition definition)
    {
        _definitions.Add(definition);
        return this;
    }

    /// <summary>Clamps the fact to the range.</summary>
    public FactDatabaseTestsBuilder WithRange(Tag fact, int min, int max)
    {
        FactDefinition definition = new() { Tag = fact, Clamp = true, Min = min, Max = max };
        return WithDefinition(definition);
    }

    public FactDatabaseTestsBuilder WithFact(Tag fact, int value)
    {
        _facts[fact] = value;
        return this;
    }

    public FactDatabase Build()
    {
        FactSettings settings = new() { Definitions = [.. _definitions] };
        FactDatabase database = new(settings);
        foreach ((Tag fact, int value) in _facts)
        {
            database.SetFact(fact, value);
        }

        return database;
    }
}
