using Arch.Core;

using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Rules;

/// <summary>Activates an ability of the subject, the instigator by default, on the context's other entity, such as an interaction offer starting a lockpick.</summary>
public sealed class ActivateAbilityEffect : Effect
{
    public Tag Ability { get; set; }

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Activate {Ability}";

    public override void Apply(ConditionContext context)
    {
        if (AbilityRuleContext.TryGetOwner(context, Subject, out AbilitySystem abilities, out Entity owner, out Entity target))
        {
            abilities.TryActivate(owner, Ability, target);
        }
    }
}
