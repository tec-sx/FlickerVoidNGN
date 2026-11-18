using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests.Tags;

public class OwnedTagsTests
{
    [Theory]
    [InlineData("Test.FVFramework.Group.One", true)]
    [InlineData("Test.FVFramework.Group", true)]
    [InlineData("Test.FVFramework.Group.Two", false)]
    [InlineData("Test.FVFramework.A", false)]
    public void MatchesTagsItOwnsAndTheirParents(string tag, bool expected)
    {
        OwnedTags owned = new([TestTags.GroupOne, TestTags.B]);
        Tag wanted = new(tag);

        bool matches = owned.HasMatchingTag(wanted);

        matches.Should().Be(expected);
    }
}
