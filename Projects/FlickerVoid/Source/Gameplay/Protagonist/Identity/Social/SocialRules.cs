using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;
using EntityIdentity = FlickerVoid.Engine.Gameplay.Entities.Identity;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social;

/// <summary>Social rules read from the settings, the facts and the entity's identity and owned tags.</summary>
public sealed class SocialRules(SocialSettings settings, IFactDatabase facts) : ISocialRules
{
    public CharacterDefinition? GetCharacter(IEntityView? entity)
    {
        if (entity is null || !entity.TryGet(out EntityIdentity identity))
        {
            return null;
        }

        return identity.Definition;
    }

    public FactionDefinition? GetFaction(IEntityView? entity)
    {
        CharacterDefinition? character = GetCharacter(entity);
        SocialFragment? social = character?.FindFragment<SocialFragment>();
        return social?.Faction;
    }

    public ScaleDefinition? GetRelationshipScale(CharacterDefinition? character)
    {
        SocialFragment? social = character?.FindFragment<SocialFragment>();
        return social?.Relationship;
    }

    public bool IsDisguised(IEntityView? entity) => HasTag(entity, settings.DisguiseRoot);

    public bool IsDisguisedAs(IEntityView? entity, FactionDefinition? faction)
    {
        if (faction is null || !HasTag(entity, faction.DisguiseTag))
        {
            return false;
        }

        int notoriety = GetNotoriety(faction);
        return notoriety < faction.DisguiseNotorietyLimit;
    }

    public FactionDefinition? GetDisguiseFaction(IEntityView? entity)
    {
        foreach (FactionDefinition faction in settings.Factions)
        {
            if (IsDisguisedAs(entity, faction))
            {
                return faction;
            }
        }

        return null;
    }

    public int GetRecognizedFame(IEntityView? entity)
    {
        int fame = GetValue(settings.Fame);
        return Recognize(entity, fame);
    }

    public int GetRecognizedNotoriety(IEntityView? entity, FactionDefinition? faction)
    {
        int notoriety = GetNotoriety(faction);
        return Recognize(entity, notoriety);
    }

    public Attitude GetAttitude(FactionDefinition? faction, IEntityView? subject)
    {
        if (faction is null || subject is null)
        {
            return Attitude.Neutral;
        }

        FactionDefinition? subjectFaction = GetFaction(subject);
        if (subjectFaction == faction || IsDisguisedAs(subject, faction))
        {
            return Attitude.Allied;
        }

        int notoriety = GetRecognizedNotoriety(subject, faction);
        if (notoriety >= faction.HostileNotoriety)
        {
            return Attitude.Hostile;
        }

        int standing = faction.Standing.GetValue(facts);
        return faction.GetStandingAttitude(standing);
    }

    private static bool HasTag(IEntityView? entity, Tag tag)
    {
        if (entity is null || !tag.IsValid || !entity.TryGet(out OwnedTags owned))
        {
            return false;
        }

        return owned.HasMatchingTag(tag);
    }

    /// <summary>Rounds half up, as Unreal does, so ports give the same values.</summary>
    private static int RoundToInt(float value) => (int)MathF.Floor(value + 0.5f);

    private int GetValue(ScaleDefinition? scale)
    {
        if (scale is null)
        {
            return 0;
        }

        return scale.GetValue(facts);
    }

    private int GetNotoriety(FactionDefinition? faction)
    {
        int global = GetValue(settings.Notoriety);
        int factionNotoriety = GetValue(faction?.Notoriety);
        return Math.Max(factionNotoriety, global);
    }

    private int Recognize(IEntityView? entity, int value)
    {
        if (!IsDisguised(entity))
        {
            return value;
        }

        return RoundToInt(value * settings.DisguiseRecognition);
    }
}
