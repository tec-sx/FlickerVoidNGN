using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Tests;

public sealed class WorldClockTests
{
    private const float Precision = 1e-4f;
    private static readonly TimeSpan HalfAMinute = TimeSpan.FromSeconds(30);

    private readonly WorldClockTestsBuilder _builder = new();

    [Fact]
    public void StartsAtMidnightOfDayZeroAndWritesNoFacts()
    {
        WorldClock clock = _builder.Build();

        clock.Hour.Should().Be(0);
        clock.Day.Should().Be(0);
        _builder.Facts.AllFacts.Should().BeEmpty("a game that never moves its clock keeps its facts to itself");
    }

    [Fact]
    public void AdvancesByItsSpeed()
    {
        WorldClock clock = _builder.At(1, 9).WithSpeed(2).Build();

        clock.Tick(HalfAMinute);

        clock.Hour.Should().BeApproximately(10, Precision);
        clock.Day.Should().Be(1);
    }

    [Fact]
    public void StandsStillAtSpeedZero()
    {
        WorldClock clock = _builder.At(1, 9).Build();

        clock.Tick(HalfAMinute);

        clock.Hour.Should().Be(9);
    }

    [Fact]
    public void KeepsTimeOverManySmallSteps()
    {
        WorldClock clock = _builder.At(1, 20).WithSpeed(0.01f).Build();
        TimeSpan step = TimeSpan.FromSeconds(1.0 / 60);

        for (int index = 0; index < 36000; index++)
        {
            clock.Tick(step);
        }

        clock.Hour.Should().BeApproximately(20.1f, Precision, "ten real minutes at a hundredth of an hour each");
    }

    [Fact]
    public void RollsIntoTheNextDayAtMidnight()
    {
        WorldClock clock = _builder.At(1, 23.5).WithSpeed(2).Build();

        clock.Tick(HalfAMinute);

        clock.Day.Should().Be(2);
        clock.Hour.Should().BeApproximately(0.5f, Precision);
    }

    [Fact]
    public void RollsHoursSetOutsideTheDayIntoDays()
    {
        WorldClock clock = _builder.Build();

        clock.Set(3, 50.25);
        int laterDay = clock.Day;
        double laterHour = clock.Hour;
        clock.Set(3, -1);

        laterDay.Should().Be(5);
        laterHour.Should().BeApproximately(2.25f, Precision);
        clock.Day.Should().Be(2);
        clock.Hour.Should().BeApproximately(23, Precision);
    }

    [Fact]
    public void TellsTheFactsItsHourAndDay()
    {
        WorldClock clock = _builder.At(1, 23.5).WithSpeed(2).Build();

        clock.Tick(HalfAMinute);

        _builder.Facts.GetFact(WorldClock.HourFact).Should().Be(0);
        _builder.Facts.GetFact(WorldClock.DayFact).Should().Be(2);
    }

    [Fact]
    public void ChangesTheFactsOnlyWhenAWholeHourPasses()
    {
        WorldClock clock = _builder.At(1, 9).WithSpeed(1).Build();
        int changes = 0;
        _builder.Facts.FactsChanged += () => changes++;

        clock.Tick(HalfAMinute);
        int withinTheHour = changes;
        clock.Tick(HalfAMinute);

        withinTheHour.Should().Be(0);
        changes.Should().Be(1);
        _builder.Facts.GetFact(WorldClock.HourFact).Should().Be(10);
    }

    [Fact]
    public void MovesToTheHourAFactEffectSets()
    {
        WorldClock clock = _builder.At(1, 9.5).Build();
        FactEffect dusk = new() { Fact = WorldClock.HourFact, Value = 20 };
        ConditionContext context = new(_builder.Facts);

        dusk.Apply(context);

        clock.Hour.Should().Be(20);
        clock.Day.Should().Be(1);
    }

    [Fact]
    public void RollsHoursAddedToItsFactPastMidnight()
    {
        WorldClock clock = _builder.At(1, 22.5).Build();

        _builder.Facts.AddFact(WorldClock.HourFact, 3);

        clock.Day.Should().Be(2);
        clock.Hour.Should().Be(1);
        _builder.Facts.GetFact(WorldClock.HourFact).Should().Be(1);
        _builder.Facts.GetFact(WorldClock.DayFact).Should().Be(2);
    }

    [Fact]
    public void KeepsItsMinutesWhenOnlyTheDayIsSet()
    {
        WorldClock clock = _builder.At(1, 9.5).Build();

        _builder.Facts.SetFact(WorldClock.DayFact, 5);

        clock.Day.Should().Be(5);
        clock.Hour.Should().BeApproximately(9.5f, Precision);
    }

    [Fact]
    public void StandsStillWhileItsHoldFactIsHeld()
    {
        WorldClock clock = _builder.At(1, 9).WithSpeed(2).Build();

        _builder.Facts.AddFact(WorldClock.HeldFact);
        clock.Tick(HalfAMinute);
        double held = clock.Hour;
        _builder.Facts.AddFact(WorldClock.HeldFact, -1);
        clock.Tick(HalfAMinute);

        held.Should().Be(9);
        clock.Hour.Should().BeApproximately(10, Precision);
    }

    [Fact]
    public void FollowsFactsRestoredFromASave()
    {
        WorldClock clock = _builder.At(1, 9.5).Build();
        Dictionary<Tag, int> saved = new() { [WorldClock.HourFact] = 14, [WorldClock.DayFact] = 3 };

        _builder.Facts.RestoreFacts(saved);

        clock.Day.Should().Be(3);
        clock.Hour.Should().Be(14);
    }

    [Fact]
    public void AConditionOnTheHourChangesAsTheHourPasses()
    {
        WorldClock clock = _builder.At(1, 19.75).WithSpeed(1).Build();
        FactCondition night = new() { Fact = WorldClock.HourFact, Compare = FactCompare.GreaterOrEqual, Value = 20 };
        ConditionContext context = new(_builder.Facts);

        bool before = night.Check(context);
        clock.Tick(HalfAMinute);
        bool after = night.Check(context);

        before.Should().BeFalse();
        after.Should().BeTrue();
    }
}
