using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
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
    private readonly SocialSettings _settings;

    public TitlesTests()
    {
        FactSettings factSettings = new() { Definitions = [new FactDefinition { Tag = FameFact, Clamp = true, Min = 0, Max = 100 }] };
        _facts = new FactDatabase(factSettings);
        _known = FameTitle("Known", 20);
        _famous = FameTitle("Famous", 50);
        _settings = new SocialSettings { Fame = _fame, Titles = [_known, _famous] };
    }

    [Fact]
    public void EarnsTitleOnceWhenItsConditionsHold()
    {
        TitleDefinition rewarded = FameTitle("Rewarded", 20, new FactEffect { Fact = RewardFact, Write = FactWrite.Add, Value = 1 });
        SocialSettings settings = new() { Titles = [rewarded] };
        using Titles titles = new(_facts, settings);
        List<TitleDefinition> earned = [];
        titles.TitleEarned += earned.Add;

        _fame.Modify(_facts, 30);
        _fame.Modify(_facts, 5);

        Assert.Equal([rewarded], earned);
        Assert.Equal(rewarded, titles.CurrentTitle);
        Assert.Equal(1, _facts.GetFact(RewardFact));
    }

    [Fact]
    public void MakesHardestTitleCurrent()
    {
        using Titles titles = new(_facts, _settings);

        _fame.Modify(_facts, 60);

        List<TitleDefinition> held = titles.GetHeldTitles();
        Assert.Equal([_famous, _known], held);
    }

    [Fact]
    public void KeepsTitlesAndMovesCurrentToNewLeader()
    {
        using Titles titles = new(_facts, _settings);
        List<TitleDefinition> changes = [];
        titles.CurrentTitleChanged += changes.Add;

        _fame.Modify(_facts, 60);
        _fame.Modify(_facts, -40);

        List<TitleDefinition> held = titles.GetHeldTitles();
        Assert.Equal([_known, _famous], held);
        Assert.Equal([_famous, _known], changes);
    }

    [Fact]
    public void KeepsPromotedTitleCurrentUntilLeaderChanges()
    {
        using Titles titles = new(_facts, _settings);
        _fame.Modify(_facts, 60);

        titles.PromoteTitle(_known);
        _facts.SetFact(RewardFact, 1);
        TitleDefinition? afterOtherChange = titles.CurrentTitle;

        _fame.Modify(_facts, -30);
        TitleDefinition? afterLeaderMatches = titles.CurrentTitle;

        _fame.Modify(_facts, 40);
        TitleDefinition? afterLeaderChanges = titles.CurrentTitle;

        Assert.Equal(_known, afterOtherChange);
        Assert.Equal(_known, afterLeaderMatches);
        Assert.Equal(_famous, afterLeaderChanges);
    }

    [Fact]
    public void EarnsUnheldTitleWhenPromoted()
    {
        TitleDefinition rewarded = FameTitle("Rewarded", 90, new FactEffect { Fact = RewardFact, Value = 7 });
        SocialSettings settings = new() { Titles = [rewarded] };
        using Titles titles = new(_facts, settings);
        List<TitleDefinition> earned = [];
        titles.TitleEarned += earned.Add;

        titles.PromoteTitle(rewarded);

        Assert.Equal([rewarded], earned);
        Assert.Equal(rewarded, titles.CurrentTitle);
        Assert.Equal(7, _facts.GetFact(RewardFact));
    }

    [Fact]
    public void ListsHeldTitlesFromCurrentToNeverCurrent()
    {
        TitleDefinition noticed = FameTitle("Noticed", 10);
        SocialSettings settings = new() { Titles = [noticed, _known, _famous] };
        using Titles titles = new(_facts, settings);

        _fame.Modify(_facts, 60);
        titles.PromoteTitle(noticed);

        List<TitleDefinition> held = titles.GetHeldTitles();
        Assert.Equal([noticed, _famous, _known], held);
    }

    [Fact]
    public void KeepsPromotedTitleAfterLoad()
    {
        _facts.SetFact(FameFact, 60);
        _facts.SetFact(_famous.Id, 2);
        _facts.SetFact(_known.Id, 3);

        using Titles titles = new(_facts, _settings);

        Assert.Equal(_known, titles.CurrentTitle);
    }

    [Fact]
    public void EarnsTitlesUnlockedByAnotherTitlesEffects()
    {
        TitleDefinition known = FameTitle("Known", 20, new ModifyScaleEffect { Scale = _fame, Delta = 40 });
        SocialSettings settings = new() { Titles = [known, _famous] };
        using Titles titles = new(_facts, settings);

        _fame.Modify(_facts, 20);

        bool knownEarned = known.IsEarned(_facts);
        Assert.True(knownEarned);
        Assert.Equal(_famous, titles.CurrentTitle);
    }

    [Fact]
    public void StopsTrackingWhenDisposed()
    {
        Titles titles = new(_facts, _settings);
        titles.Dispose();

        _fame.Modify(_facts, 60);

        List<TitleDefinition> held = titles.GetHeldTitles();
        Assert.Empty(held);
    }

    [Fact]
    public void HasTitleConditionReadsEarnedTitles()
    {
        HasTitleCondition hasKnown = new() { Title = _known };
        ConditionContext context = new(_facts);
        bool before = hasKnown.Check(context);

        _facts.SetFact(_known.Id, 1);
        bool after = hasKnown.Check(context);
        string description = hasKnown.Describe();

        Assert.False(before);
        Assert.True(after);
        Assert.Equal("Has title Known", description);
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
