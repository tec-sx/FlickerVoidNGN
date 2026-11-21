using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>Gives items to the subject, the instigator by default, ignoring carry limits so a story reward is never lost.</summary>
public sealed class GiveItemEffect : Effect
{
    /// <summary>The <see cref="ItemDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Give {InventoryContext.Name(Item)} x{Quantity}";

    public override void Apply(ConditionContext context)
    {
        ItemDefinition? item = InventoryContext.LoadItem(context, Item);
        if (item is null || !InventoryContext.TryGetOwner(context, Subject, out InventorySystem inventories, out Entity owner))
        {
            return;
        }

        inventories.AddStoryItem(owner, item, Quantity);
    }
}
