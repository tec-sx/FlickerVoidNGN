using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>The subject, the instigator by default, holds at least the quantity of the item.</summary>
public sealed class HasItemCondition : Condition
{
    /// <summary>The <see cref="ItemDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Has {InventoryContext.Name(Item)} x{Quantity}";

    protected override bool Evaluate(ConditionContext context)
    {
        ItemDefinition? item = InventoryContext.LoadItem(context, Item);
        if (item is null || !InventoryContext.TryGetOwner(context, Subject, out InventorySystem inventories, out Entity owner))
        {
            return false;
        }

        return inventories.HasItem(owner, item, Quantity);
    }
}
