using Arch.Core;

using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Abilities.Rules;

/// <summary>What the ability rules find in their context: the ability system, the subject that owns the ability and the other entity it acts on.</summary>
internal static class AbilityRuleContext
{
    public static bool TryGetOwner(ConditionContext context, ContextEntity subject, out AbilitySystem abilities, out Entity owner, out Entity target)
    {
        AbilitySystem? system = context.GetService<AbilitySystem>();
        ContextEntity other = subject == ContextEntity.Instigator ? ContextEntity.Target : ContextEntity.Instigator;
        target = context.GetEntity(other) is WorldEntityView targetView ? targetView.Entity : Entity.Null;
        if (system is null || context.GetEntity(subject) is not WorldEntityView view)
        {
            abilities = null!;
            owner = Entity.Null;
            return false;
        }

        abilities = system;
        owner = view.Entity;
        return true;
    }
}
