using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>Takes money from the subject, the instigator by default; takes nothing when there isn't enough.</summary>
public sealed class TakeMoneyEffect : Effect
{
    public int Amount { get; set; } = 1;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Take {Amount} money";

    public override void Apply(ConditionContext context)
    {
        if (!InventoryContext.TryGetOwner(context, Subject, out InventorySystem inventories, out Entity owner))
        {
            return;
        }

        inventories.RemoveMoney(owner, Amount);
    }
}
