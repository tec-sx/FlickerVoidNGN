using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Story.Knowledge;

namespace FlickerVoid.Engine.Story.Tests.Knowledge;

public sealed class KnowledgeSystemTests : IDisposable
{
    private static readonly Tag Blood = new("Knowledge.Clue.BloodOnCoat");
    private static readonly Tag Killer = new("Knowledge.Thought.TheButlerLied");
    private static readonly Tag Suspicion = new("Fact.Butler.Suspicion");

    private readonly KnowledgeSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void LearningSetsItsFactAndAppliesItsEffects()
    {
        _builder.WithKnowledge(Blood.Name, """
            "onLearned": { "effects": [ { "$type": "fact", "fact": "Fact.Butler.Suspicion", "value": 2 } ] }
            """).Build();

        bool learned = _builder.Knowledge.Learn(Blood);

        learned.Should().BeTrue();
        _builder.Knowledge.Knows(Blood).Should().BeTrue();
        _builder.Game.Facts.GetFact(Blood).Should().Be(1);
        _builder.Game.Facts.GetFact(Suspicion).Should().Be(2);
    }

    [Fact]
    public void WhatIsKnownIsNotLearnedAgain()
    {
        _builder.WithKnowledge(Blood.Name, """
            "onLearned": { "effects": [ { "$type": "fact", "fact": "Fact.Butler.Suspicion", "write": "Add" } ] }
            """).Build();
        _builder.Knowledge.Learn(Blood);

        bool again = _builder.Knowledge.Learn(Blood);

        again.Should().BeFalse();
        _builder.Game.Facts.GetFact(Suspicion).Should().Be(1);
    }

    [Fact]
    public void ItNeedsItsPrerequisitesKnownFirst()
    {
        _builder.WithKnowledge(Blood.Name)
            .WithKnowledge(Killer.Name, $$"""
                "kind": "Thought", "prerequisites": [ "{{Blood.Name}}" ]
                """)
            .Build();

        bool early = _builder.Knowledge.Learn(Killer);
        _builder.Knowledge.Learn(Blood);
        bool canLearn = _builder.Knowledge.CanLearn(Killer);

        early.Should().BeFalse();
        canLearn.Should().BeTrue();
    }

    [Fact]
    public void UnknownKnowledgeCannotBeLearned()
    {
        _builder.Build();

        bool learned = _builder.Knowledge.Learn(Blood);

        learned.Should().BeFalse();
        _builder.Game.Facts.IsFactDefined(Blood).Should().BeFalse();
    }

    [Fact]
    public void ForgettingRemovesItsFact()
    {
        _builder.WithKnowledge(Blood.Name).Build();
        _builder.Knowledge.Learn(Blood);

        _builder.Knowledge.Forget(Blood);

        _builder.Knowledge.Knows(Blood).Should().BeFalse();
        _builder.Game.Facts.IsFactDefined(Blood).Should().BeFalse();
    }

    [Fact]
    public void ListsWhatIsKnownOfTheGivenKinds()
    {
        _builder.WithKnowledge(Blood.Name)
            .WithKnowledge(Killer.Name, """ "kind": "Thought" """)
            .WithKnowledge("Knowledge.Topic.Weather", """ "kind": "Topic" """)
            .Build();
        _builder.Knowledge.Learn(Blood);
        _builder.Knowledge.Learn(Killer);

        IReadOnlyList<KnowledgeDefinition> thoughts = _builder.Knowledge.GetKnown(KnowledgeKind.Thought);
        IReadOnlyList<KnowledgeDefinition> all = _builder.Knowledge.GetKnown();

        thoughts.Select(known => known.Id).Should().Equal(Killer);
        all.Select(known => known.Id).Should().BeEquivalentTo([Blood, Killer]);
    }

    [Fact]
    public void TheKnowsConditionAndTheLearnEffectWorkFromContent()
    {
        _builder.WithKnowledge(Blood.Name).Build();
        Condition knows = _builder.Read<Condition>($$"""{ "$type": "knows", "knowledge": "{{Blood.Name}}" }""");
        Effect learn = _builder.Read<Effect>($$"""{ "$type": "learn", "knowledge": "{{Blood.Name}}" }""");

        bool before = knows.Check(_builder.Context);
        learn.Apply(_builder.Context);
        bool after = knows.Check(_builder.Context);

        before.Should().BeFalse();
        after.Should().BeTrue();
    }
}
