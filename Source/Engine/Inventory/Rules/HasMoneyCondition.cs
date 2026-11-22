using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>The subject, the instigator by default, has at least the amount of money.</summary>
public sealed class HasMoneyCondition : Condition
{
    public int Amount { get; set; } = 1;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Has {Amount} money";

    protected override bool Evaluate(ConditionContext context)
    {
        if (!InventoryContext.TryGetOwner(context, Subject, out InventorySystem inventories, out Entity owner))
        {
            return false;
        }

        return inventories.CanAfford(owner, Amount);
    }
}
