using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests.Tags;

public class TagTests
{
    [Fact]
    public void EqualsIgnoringCase()
    {
        Tag lower = new("fact.social.fame");
        Tag mixed = new("Fact.Social.Fame");

        int lowerHash = lower.GetHashCode();
        int mixedHash = mixed.GetHashCode();

        lower.Should().Be(mixed);
        lowerHash.Should().Be(mixedHash);
    }

    [Fact]
    public void MatchesItselfAndItsParents()
    {
        bool itself = TestTags.GroupOne.MatchesTag(TestTags.GroupOne);
        bool parent = TestTags.GroupOne.MatchesTag(TestTags.Group);
        bool child = TestTags.Group.MatchesTag(TestTags.GroupOne);

        itself.Should().BeTrue();
        parent.Should().BeTrue();
        child.Should().BeFalse();
    }

    [Fact]
    public void MatchesParentsOnlyAtDots()
    {
        Tag partial = new("Test.FVFramework.Gro");

        bool matches = TestTags.GroupOne.MatchesTag(partial);

        matches.Should().BeFalse();
    }

    [Fact]
    public void NoneIsInvalidAndMatchesNothing()
    {
        bool noneMatchesNone = Tag.None.MatchesTag(Tag.None);
        bool tagMatchesNone = TestTags.A.MatchesTag(Tag.None);

        Tag.None.IsValid.Should().BeFalse();
        noneMatchesNone.Should().BeFalse();
        tagMatchesNone.Should().BeFalse();
    }
}
