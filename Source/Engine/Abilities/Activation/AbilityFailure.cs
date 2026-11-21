using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>Why an ability didn't activate, as tags its failure messages name.</summary>
public static class AbilityFailure
{
    /// <summary>The entity has no such ability.</summary>
    public static readonly Tag Missing = new("Ability.Failure.Missing");

    /// <summary>It is already running.</summary>
    public static readonly Tag Active = new("Ability.Failure.Active");

    /// <summary>A running ability blocks its tags.</summary>
    public static readonly Tag Blocked = new("Ability.Failure.Blocked");

    /// <summary>The owner lacks a required tag.</summary>
    public static readonly Tag TagsMissing = new("Ability.Failure.TagsMissing");

    /// <summary>The owner has a blocked tag.</summary>
    public static readonly Tag TagsBlocked = new("Ability.Failure.TagsBlocked");

    /// <summary>An exclusive ability that blocks others is running.</summary>
    public static readonly Tag Group = new("Ability.Failure.Group");

    public static readonly Tag Conditions = new("Ability.Failure.Conditions");

    public static readonly Tag Cost = new("Ability.Failure.Cost");
}
