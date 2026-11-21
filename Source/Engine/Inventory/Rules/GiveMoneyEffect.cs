using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>Gives money to the subject, the instigator by default.</summary>
public sealed class GiveMoneyEffect : Effect
{
    public int Amount { get; set; } = 1;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Give {Amount} money";

    public override void Apply(ConditionContext context)
    {
        if (!InventoryContext.TryGetOwner(context, Subject, out InventorySystem inventories, out Entity owner))
        {
            return;
        }

        inventories.AddMoney(owner, Amount);
    }
}
