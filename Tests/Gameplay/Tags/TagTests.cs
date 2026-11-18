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

        Assert.Equal(mixed, lower);
        Assert.Equal(mixedHash, lowerHash);
    }

    [Fact]
    public void MatchesItselfAndItsParents()
    {
        bool itself = TestTags.GroupOne.MatchesTag(TestTags.GroupOne);
        bool parent = TestTags.GroupOne.MatchesTag(TestTags.Group);
        bool child = TestTags.Group.MatchesTag(TestTags.GroupOne);

        Assert.True(itself);
        Assert.True(parent);
        Assert.False(child);
    }

    [Fact]
    public void MatchesParentsOnlyAtDots()
    {
        Tag partial = new("Test.FVFramework.Gro");

        bool matches = TestTags.GroupOne.MatchesTag(partial);

        Assert.False(matches);
    }

    [Fact]
    public void NoneIsInvalidAndMatchesNothing()
    {
        bool noneMatchesNone = Tag.None.MatchesTag(Tag.None);
        bool tagMatchesNone = TestTags.A.MatchesTag(Tag.None);

        Assert.False(Tag.None.IsValid);
        Assert.False(noneMatchesNone);
        Assert.False(tagMatchesNone);
    }
}
