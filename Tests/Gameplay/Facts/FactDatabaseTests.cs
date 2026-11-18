using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests.Facts;

public class FactDatabaseTests
{
    private readonly FactDatabaseTestsBuilder _builder = new();

    [Fact]
    public void ReadsZeroForUndefinedFact()
    {
        FactDatabase facts = _builder.Build();

        bool defined = facts.IsFactDefined(TestTags.A);
        int value = facts.GetFact(TestTags.A);

        defined.Should().BeFalse();
        value.Should().Be(0);
    }

    [Fact]
    public void SetsFact()
    {
        FactDatabase facts = _builder.Build();

        facts.SetFact(TestTags.A, 5);

        bool defined = facts.IsFactDefined(TestTags.A);
        int value = facts.GetFact(TestTags.A);
        defined.Should().BeTrue();
        value.Should().Be(5);
    }

    [Fact]
    public void AddsDeltaOrOne()
    {
        FactDatabase facts = _builder.WithFact(TestTags.A, 5).Build();

        facts.AddFact(TestTags.A, 3);
        facts.AddFact(TestTags.B);

        int a = facts.GetFact(TestTags.A);
        int b = facts.GetFact(TestTags.B);
        a.Should().Be(8);
        b.Should().Be(1);
    }

    [Fact]
    public void RemovesOnlyDefinedFacts()
    {
        FactDatabase facts = _builder.WithFact(TestTags.A, 5).Build();

        bool removed = facts.RemoveFact(TestTags.A);
        bool removedTwice = facts.RemoveFact(TestTags.A);

        bool defined = facts.IsFactDefined(TestTags.A);
        removed.Should().BeTrue();
        removedTwice.Should().BeFalse();
        defined.Should().BeFalse();
    }

    [Fact]
    public void RemovesOnlyFactsUnderParentTag()
    {
        FactDatabase facts = _builder
            .WithFact(TestTags.GroupOne, 1)
            .WithFact(TestTags.GroupTwo, 2)
            .WithFact(TestTags.A, 3)
            .Build();

        int removed = facts.RemoveFactsUnder(TestTags.Group);

        removed.Should().Be(2);
        facts.AllFacts.Keys.Should().ContainSingle().Which.Should().Be(TestTags.A);
    }

    [Fact]
    public void NotifiesListenersWithOldAndNewValue()
    {
        FactDatabase facts = _builder.WithFact(TestTags.A, 2).Build();
        FactChangedHandler handler = Substitute.For<FactChangedHandler>();
        facts.FactChanged += handler;

        facts.AddFact(TestTags.A, 3);
        facts.RemoveFact(TestTags.A);

        Received.InOrder(() =>
        {
            handler(TestTags.A, 2, 5);
            handler(TestTags.A, 5, 0);
        });
        facts.LastChangedTag.Should().Be(TestTags.A);
    }

    [Theory]
    [InlineData(50, 10)]
    [InlineData(-100, -10)]
    public void ClampsFactWithRangeInSettings(int value, int expected)
    {
        FactDatabase facts = _builder.WithRange(TestTags.A, -10, 10).Build();

        facts.SetFact(TestTags.A, value);

        int clamped = facts.GetFact(TestTags.A);
        clamped.Should().Be(expected);
    }

    [Fact]
    public void ResetsToClampedDefaultsWithValueNames()
    {
        FactDatabase facts = _builder
            .WithDefinition(new FactDefinition { Tag = TestTags.A, DefaultValue = 1, ValueNames = ["Closed", "Open"] })
            .WithDefinition(new FactDefinition { Tag = TestTags.B, DefaultValue = 50, Clamp = true, Min = 0, Max = 10 })
            .WithFact(TestTags.GroupOne, 1)
            .Build();

        facts.ResetToDefaults();

        string? valueName = facts.GetFactValueName(TestTags.A);
        valueName.Should().Be("Open");
        facts.AllFacts.Should().Equal(new Dictionary<Tag, int> { [TestTags.A] = 1, [TestTags.B] = 10 });
    }

    [Fact]
    public void RefusesToWriteNoTag()
    {
        FactDatabase facts = _builder.Build();

        Action write = () => facts.SetFact(Tag.None, 1);

        write.Should().Throw<ArgumentException>();
    }
}
