using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Relationships;

public class GiftsFragmentTests
{
    private readonly GiftsFragment _gifts = new()
    {
        Preferences =
        [
            new GiftPreference { Tag = new Tag("Item.Food"), RelationshipChange = 2 },
            new GiftPreference { Tag = new Tag("Item.Food.Sweet"), RelationshipChange = 3 },
            new GiftPreference { Tag = new Tag("Item.Weapon"), RelationshipChange = -5 },
        ],
        DefaultRelationshipChange = 1,
    };

    [Fact]
    public void AddsUpMatchingPreferencesIncludingParents()
    {
        Tag[] cake = [new("Item.Food.Sweet.Cake")];

        int change = _gifts.GetRelationshipChange(cake);

        Assert.Equal(5, change);
    }

    [Fact]
    public void UsesDefaultWhenNothingMatches()
    {
        Tag[] flower = [new("Item.Flower")];

        int change = _gifts.GetRelationshipChange(flower);

        Assert.Equal(1, change);
    }

    [Fact]
    public void KeepsDislikesNegative()
    {
        Tag[] knife = [new("Item.Weapon.Knife"), new("Item.Tool")];

        int change = _gifts.GetRelationshipChange(knife);

        Assert.Equal(-5, change);
    }
}
