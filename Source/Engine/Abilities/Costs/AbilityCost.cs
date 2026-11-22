using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Abilities.Costs;

/// <summary>What an ability spends when it activates, with its owner as the context's instigator; shared by everything that uses the asset.</summary>
public abstract class AbilityCost
{
    public abstract bool CanPay(ConditionContext context);

    public abstract void Pay(ConditionContext context);
}
