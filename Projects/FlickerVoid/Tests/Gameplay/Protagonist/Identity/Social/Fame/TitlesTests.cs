using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Fame;

public class TitlesTests
{
    private static readonly Tag FameFact = new("Fact.Social.Fame");
    private static readonly Tag RewardFact = new("Fact.Test.Reward");

    private readonly FactDatabase _facts;
    private readonly ScaleDefinition _fame = new() { Id = FameFact };
    private readonly TitleDefinition _known;
    private readonly TitleDefinition _famous;
    private readonly TitlesTestsBuilder _builder;

    public TitlesTests()
    {
        FactSettings settings = new() { Definitions = [new FactDefinition { Tag = FameFact, Clamp = true, Min = 0, Max = 100 }] };
        _facts = new FactDatabase(settings);
        _known = FameTitle("Known", 20);
        _famous = FameTitle("Famous", 50);
        _builder = new TitlesTestsBuilder(_facts);
    }

    [Fact]
    public void EarnsTitleOnceWhenItsConditionsHold()
    {
        TitleDefinition rewarded = FameTitle("Rewarded", 20, new FactEffect { Fact = RewardFact, Write = FactWrite.Add, Value = 1 });
        using Titles titles = _builder.WithTitles(rewarded).Build();
        List<TitleDefinition> earned = [];
        titles.TitleEarned += earned.Add;

        _fame.Modify(_facts, 30);
        _fame.Modify(_facts, 5);

        int reward = _facts.GetFact(RewardFact);
        earned.Should().Equal(rewarded);
        titles.CurrentTitle.Should().Be(rewarded);
        reward.Should().Be(1);
    }

    [Fact]
    public void MakesHardestTitleCurrent()
    {
        using Titles titles = _builder.WithTitles(_known, _famous).Build();

        _fame.Modify(_facts, 60);

        List<TitleDefinition> held = titles.GetHeldTitles();
        held.Should().Equal(_famous, _known);
    }

    [Fact]
    public void KeepsTitlesAndMovesCurrentToNewLeader()
    {
        using Titles titles = _builder.WithTitles(_known, _famous).Build();
        List<TitleDefinition> changes = [];
        titles.CurrentTitleChanged += changes.Add;

        _fame.Modify(_facts, 60);
        _fame.Modify(_facts, -40);

        List<TitleDefinition> held = titles.GetHeldTitles();
        held.Should().Equal(_known, _famous);
        changes.Should().Equal(_famous, _known);
    }

    [Fact]
    public void KeepsPromotedTitleCurrentUntilLeaderChanges()
    {
        using Titles titles = _builder.WithTitles(_known, _famous).Build();
        _fame.Modify(_facts, 60);

        titles.PromoteTitle(_known);
        _facts.SetFact(RewardFact, 1);
        TitleDefinition? afterOtherChange = titles.CurrentTitle;

        _fame.Modify(_facts, -30);
        TitleDefinition? afterLeaderMatches = titles.CurrentTitle;

        _fame.Modify(_facts, 40);
        TitleDefinition? afterLeaderChanges = titles.CurrentTitle;

        afterOtherChange.Should().Be(_known);
        afterLeaderMatches.Should().Be(_known);
        afterLeaderChanges.Should().Be(_famous);
    }

    [Fact]
    public void EarnsUnheldTitleWhenPromoted()
    {
        TitleDefinition rewarded = FameTitle("Rewarded", 90, new FactEffect { Fact = RewardFact, Value = 7 });
        using Titles titles = _builder.WithTitles(rewarded).Build();
        List<TitleDefinition> earned = [];
        titles.TitleEarned += earned.Add;

        titles.PromoteTitle(rewarded);

        int reward = _facts.GetFact(RewardFact);
        earned.Should().Equal(rewarded);
        titles.CurrentTitle.Should().Be(rewarded);
        reward.Should().Be(7);
    }

    [Fact]
    public void ListsHeldTitlesFromCurrentToNeverCurrent()
    {
        TitleDefinition noticed = FameTitle("Noticed", 10);
        using Titles titles = _builder.WithTitles(noticed, _known, _famous).Build();

        _fame.Modify(_facts, 60);
        titles.PromoteTitle(noticed);

        List<TitleDefinition> held = titles.GetHeldTitles();
        held.Should().Equal(noticed, _famous, _known);
    }

    [Fact]
    public void KeepsPromotedTitleAfterLoad()
    {
        _facts.SetFact(FameFact, 60);
        _facts.SetFact(_famous.Id, 2);
        _facts.SetFact(_known.Id, 3);

        using Titles titles = _builder.WithTitles(_known, _famous).Build();

        titles.CurrentTitle.Should().Be(_known);
    }

    [Fact]
    public void EarnsTitlesUnlockedByAnotherTitlesEffects()
    {
        TitleDefinition known = FameTitle("Known", 20, new ModifyScaleEffect { Scale = _fame, Delta = 40 });
        using Titles titles = _builder.WithTitles(known, _famous).Build();

        _fame.Modify(_facts, 20);

        bool knownEarned = known.IsEarned(_facts);
        knownEarned.Should().BeTrue();
        titles.CurrentTitle.Should().Be(_famous);
    }

    [Fact]
    public void StopsTrackingWhenDisposed()
    {
        Titles titles = _builder.WithTitles(_known, _famous).Build();
        titles.Dispose();

        _fame.Modify(_facts, 60);

        List<TitleDefinition> held = titles.GetHeldTitles();
        held.Should().BeEmpty();
    }

    private TitleDefinition FameTitle(string name, int fame, params Effect[] onEarned)
    {
        ScaleCondition condition = new() { Scale = _fame, Value = fame };
        return new TitleDefinition
        {
            Id = new Tag($"Fact.Title.{name}"),
            Display = new() { Name = name },
            Conditions = new ConditionSet { Conditions = [condition] },
            OnEarned = new EffectList { Effects = [.. onEarned] },
        };
    }
}
