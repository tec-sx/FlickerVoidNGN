using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Relationships;

public class GiftsFragmentTests
{
    private readonly GiftsFragment _gifts = new GiftsFragmentTestsBuilder()
        .WithPreference("Item.Food", 2)
        .WithPreference("Item.Food.Sweet", 3)
        .WithPreference("Item.Weapon", -5)
        .WithDefaultChange(1)
        .Build();

    [Fact]
    public void AddsUpMatchingPreferencesIncludingParents()
    {
        Tag[] cake = [new("Item.Food.Sweet.Cake")];

        int change = _gifts.GetRelationshipChange(cake);

        change.Should().Be(5);
    }

    [Fact]
    public void UsesDefaultWhenNothingMatches()
    {
        Tag[] flower = [new("Item.Flower")];

        int change = _gifts.GetRelationshipChange(flower);

        change.Should().Be(1);
    }

    [Fact]
    public void KeepsDislikesNegative()
    {
        Tag[] knife = [new("Item.Weapon.Knife"), new("Item.Tool")];

        int change = _gifts.GetRelationshipChange(knife);

        change.Should().Be(-5);
    }
}
