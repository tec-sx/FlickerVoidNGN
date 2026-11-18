using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social;

/// <summary>Game-wide social setup: the fame and notoriety scales, the titles, factions and characters the game tracks, and disguises.</summary>
public sealed class SocialSettings
{
    /// <summary>How widely the player is talked about, whatever factions think of them; faction standing scales feed it through their links.</summary>
    public ScaleDefinition? Fame { get; init; }

    /// <summary>How wanted the player is overall; faction standing scales feed it through their links.</summary>
    public ScaleDefinition? Notoriety { get; init; }

    /// <summary>Titles the player can earn; they are checked whenever a fact changes.</summary>
    public List<TitleDefinition> Titles { get; init; } = [];

    /// <summary>Factions the menus, disguises and debug commands know about.</summary>
    public List<FactionDefinition> Factions { get; init; } = [];

    /// <summary>Characters the menus list once their relationship scale has been set.</summary>
    public List<CharacterDefinition> Relationships { get; init; } = [];

    /// <summary>Wearing anything tagged under this root hides who the player is.</summary>
    public Tag DisguiseRoot { get; init; }

    /// <summary>Fame and notoriety others read while the player is disguised, as a fraction of the real value.</summary>
    public float DisguiseRecognition { get; init; } = 0.25f;

    /// <summary>Characters from Relationships whose relationship has been set.</summary>
    public List<CharacterDefinition> GetKnownCharacters(IFactDatabase facts)
    {
        List<CharacterDefinition> known = [];
        foreach (CharacterDefinition character in Relationships)
        {
            SocialFragment? social = character.FindFragment<SocialFragment>();
            if (social?.Relationship is { } relationship && facts.IsFactDefined(relationship.Id))
            {
                known.Add(character);
            }
        }

        return known;
    }
}
