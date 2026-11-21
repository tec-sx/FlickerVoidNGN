using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>Takes items from the subject, the instigator by default; takes nothing when there aren't enough.</summary>
public sealed class TakeItemEffect : Effect
{
    /// <summary>The <see cref="ItemDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Take {InventoryContext.Name(Item)} x{Quantity}";

    public override void Apply(ConditionContext context)
    {
        ItemDefinition? item = InventoryContext.LoadItem(context, Item);
        if (item is null || !InventoryContext.TryGetOwner(context, Subject, out InventorySystem inventories, out Entity owner))
        {
            return;
        }

        inventories.RemoveItem(owner, item, Quantity);
    }
}
