using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

/// <summary>A faction: the player's standing and notoriety with it are scales, and the standing scale's links feed fame, global notoriety and related factions.</summary>
public sealed class FactionDefinition : Definition
{
    /// <summary>The player's standing with the faction; its bands name the tiers.</summary>
    public required ScaleDefinition Standing { get; init; }

    /// <summary>Attitude per standing band tag; below every band, or for a band not listed, members are Neutral.</summary>
    public Dictionary<Tag, Attitude> BandAttitudes { get; init; } = [];

    /// <summary>How wanted the player is by this faction.</summary>
    public ScaleDefinition? Notoriety { get; init; }

    /// <summary>At or above this notoriety, as the faction reads it, members are hostile regardless of standing.</summary>
    public int HostileNotoriety { get; init; } = 75;

    /// <summary>Tag (granted by outfits) that makes someone pass as a member.</summary>
    public Tag DisguiseTag { get; init; }

    /// <summary>Disguises stop working at or above this notoriety, faction or global.</summary>
    public int DisguiseNotorietyLimit { get; init; } = 50;

    /// <summary>Attitude members take toward someone at this standing, from the standing band reached.</summary>
    public Attitude GetStandingAttitude(int standing)
    {
        ScaleBand? band = Standing.FindBand(standing);
        if (band is null || !BandAttitudes.TryGetValue(band.Tag, out Attitude attitude))
        {
            return Attitude.Neutral;
        }

        return attitude;
    }
}
