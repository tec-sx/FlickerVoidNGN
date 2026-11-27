using FlickerVoid.Engine.Attributes.Checks;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Localization;

namespace FlickerVoid.Engine.Attributes.Tests.Checks;

public sealed class CheckSystemTests : IDisposable
{
    /// <summary>Ways to roll at least 2 to 12 on 2d6, out of 36, worked out by hand rather than by the code under test.</summary>
    private static readonly int[] TwoDiceAtLeast = [36, 35, 33, 30, 26, 21, 15, 10, 6, 3, 1];

    private readonly CheckSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheChanceMatchesTheTwoDiceTable()
    {
        for (int needed = 2; needed <= 12; needed++)
        {
            float chance = CheckSystem.ChanceAtLeast(CheckRoll.D6x2, needed);
            float expected = TwoDiceAtLeast[needed - 2] / 36f;
            chance.Should().Be(expected, $"2d6 >= {needed}");
        }
    }

    [Fact]
    public void TwoDiceAreCertainBelowTwoAndImpossibleAboveTwelve()
    {
        CheckSystem.ChanceAtLeast(CheckRoll.D6x2, 0).Should().Be(1);
        CheckSystem.ChanceAtLeast(CheckRoll.D6x2, -5).Should().Be(1);
        CheckSystem.ChanceAtLeast(CheckRoll.D6x2, 13).Should().Be(0);
    }

    [Fact]
    public void SingleDiceCountTheirFaces()
    {
        CheckSystem.ChanceAtLeast(CheckRoll.D10, 1).Should().Be(1);
        CheckSystem.ChanceAtLeast(CheckRoll.D10, 10).Should().Be(0.1f);
        CheckSystem.ChanceAtLeast(CheckRoll.D10, 11).Should().Be(0);
        CheckSystem.ChanceAtLeast(CheckRoll.D20, 11).Should().Be(0.5f);
    }

    [Fact]
    public void WithoutDiceNothingCanBeNeeded()
    {
        CheckSystem.ChanceAtLeast(CheckRoll.None, 0).Should().Be(1);
        CheckSystem.ChanceAtLeast(CheckRoll.None, 1).Should().Be(0);
    }

    [Fact]
    public void TheBonusesWhoseConditionsPassAreAdded()
    {
        _builder.WithFile("Checks/Sneak.fvdata", """
            { "type": "check", "roll": "None", "modifiers": [
                { "label": { "key": "Test.Dark", "text": "Dark" }, "bonus": 2 },
                { "label": { "key": "Test.Quiet", "text": "Quiet" }, "bonus": 3 },
                { "label": { "key": "Test.Never", "text": "Never" }, "bonus": 100, "conditions": { "conditions": [ { "$type": "group", "invert": true } ] } } ] }
            """).Build();
        CheckDefinition sneak = _builder.Load("Checks/Sneak.fvdata");
        ConditionContext context = _builder.Context();

        CheckResult five = _builder.Checks.Preview(sneak, context, 5);
        CheckResult six = _builder.Checks.Preview(sneak, context, 6);

        five.Total.Should().Be(5);
        five.Breakdown.Should().Equal(new CheckLine(new LocalizedText("Test.Dark", "Dark"), 2), new CheckLine(new LocalizedText("Test.Quiet", "Quiet"), 3));
        five.Success.Should().BeTrue();
        six.Success.Should().BeFalse();
    }

    [Fact]
    public void TheInstigatorsAttributeAndTheRollAreAdded()
    {
        _builder.WithFile("Checks/Sneak.fvdata", """{ "type": "check", "attribute": "Attribute.Skill.Stealth" }""").Build(stealth: 4);
        CheckDefinition sneak = _builder.Load("Checks/Sneak.fvdata");
        ConditionContext context = _builder.Context();

        CheckResult rolled = _builder.Checks.Roll(sneak, context, 0);
        float chance = _builder.Checks.SuccessChance(sneak, context, 11);

        rolled.Breakdown.Should().HaveCount(2);
        rolled.Breakdown[0].Should().Be(new CheckLine(new LocalizedText("Test.Stealth", "Stealth"), 4));
        rolled.Breakdown[1].Label.Should().Be(CheckSystem.RollLabel);
        rolled.Breakdown[1].Value.Should().BeInRange(2, 12);
        rolled.Total.Should().Be(4 + rolled.Breakdown[1].Value);
        chance.Should().Be(21 / 36f);
    }

    [Fact]
    public void APassiveCheckPassesWhenItWouldSucceedWithoutRolling()
    {
        _builder.WithFile("Checks/Sneak.fvdata", """{ "type": "check", "attribute": "Attribute.Skill.Stealth" }""").Build(stealth: 4);
        ConditionContext context = _builder.Context();

        bool four = new PassiveCheckCondition { Definition = "Checks/Sneak.fvdata", Difficulty = 4 }.Check(context);
        bool five = new PassiveCheckCondition { Definition = "Checks/Sneak.fvdata", Difficulty = 5 }.Check(context);

        four.Should().BeTrue();
        five.Should().BeFalse();
    }
}
