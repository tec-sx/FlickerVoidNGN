using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social;

/// <summary>Reads who an entity is socially: its faction, disguise, the fame and notoriety others recognize, and how factions treat it.</summary>
public interface ISocialRules
{
    CharacterDefinition? GetCharacter(IEntityView? entity);

    FactionDefinition? GetFaction(IEntityView? entity);

    ScaleDefinition? GetRelationshipScale(CharacterDefinition? character);

    /// <summary>Wearing anything tagged under the disguise root.</summary>
    bool IsDisguised(IEntityView? entity);

    /// <summary>Wearing the faction's disguise while notoriety stays below its limit.</summary>
    bool IsDisguisedAs(IEntityView? entity, FactionDefinition? faction);

    /// <summary>The first faction the entity passes as a member of; null when no disguise works.</summary>
    FactionDefinition? GetDisguiseFaction(IEntityView? entity);

    /// <summary>Fame as others read it: a fraction of it while disguised.</summary>
    int GetRecognizedFame(IEntityView? entity);

    /// <summary>The higher of the faction's and the global notoriety, as others read it: a fraction of it while disguised.</summary>
    int GetRecognizedNotoriety(IEntityView? entity, FactionDefinition? faction);

    /// <summary>How the faction's members treat the subject.</summary>
    Attitude GetAttitude(FactionDefinition? faction, IEntityView? subject);
}
