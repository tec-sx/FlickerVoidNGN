using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>The subject, the instigator by default, holds at least the quantity of items with the tag or a tag under it, such as a key or a tool.</summary>
public sealed class HasItemWithTagCondition : Condition
{
    public Tag Tag { get; set; }

    public int Quantity { get; set; } = 1;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Has item tagged {Tag} x{Quantity}";

    protected override bool Evaluate(ConditionContext context)
    {
        if (!Tag.IsValid || !InventoryContext.TryGetOwner(context, Subject, out InventorySystem inventories, out Entity owner))
        {
            return false;
        }

        return inventories.GetQuantityWithTag(owner, Tag) >= Quantity;
    }
}
