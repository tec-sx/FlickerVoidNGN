using Arch.Core;

using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Rules;

/// <summary>The subject, the instigator by default, has the ability and could activate it on the context's other entity now.</summary>
public sealed class CanActivateAbilityCondition : Condition
{
    public Tag Ability { get; set; }

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Can {Ability}";

    protected override bool Evaluate(ConditionContext context)
    {
        if (!AbilityRuleContext.TryGetOwner(context, Subject, out AbilitySystem abilities, out Entity owner, out Entity target))
        {
            return false;
        }

        return abilities.CanActivate(owner, Ability, out _, target);
    }
}
