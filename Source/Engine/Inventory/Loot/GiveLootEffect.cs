using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Rules;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Inventory.Loot;

/// <summary>Rolls a loot table into the subject's inventory, the instigator's by default; entries see the instigator and the subject as target.</summary>
public sealed class GiveLootEffect : Effect
{
    /// <summary>The <see cref="LootTableDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Table { get; set; } = string.Empty;

    public ContextEntity Subject { get; set; }

    public override string Describe() => $"Give loot from {InventoryContext.Name(Table)}";

    public override void Apply(ConditionContext context)
    {
        LootSystem? loot = context.GetService<LootSystem>();
        if (loot is null || !InventoryContext.TryGetOwner(context, Subject, out InventorySystem _, out Entity receiver))
        {
            return;
        }

        Entity instigator = Entity.Null;
        if (context.Instigator is WorldEntityView view)
        {
            instigator = view.Entity;
        }

        loot.GiveLoot(Table, receiver, instigator);
    }
}
