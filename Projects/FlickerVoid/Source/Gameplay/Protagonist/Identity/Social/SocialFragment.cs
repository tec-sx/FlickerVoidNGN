using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social;

/// <summary>Social data of a character: the faction they belong to and the scale holding the player's relationship with them.</summary>
public sealed class SocialFragment : CharacterFragment
{
    public FactionDefinition? Faction { get; init; }

    /// <summary>Scale holding the player's relationship with this character; its fact's definition gives the range and starting value.</summary>
    public ScaleDefinition? Relationship { get; init; }
}
