using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Rules;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>The subject, the instigator by default, wears the item, or with no item anything that grants the tag or a tag under it.</summary>
public sealed class IsWearingCondition : Condition
{
    /// <summary>The <see cref="ItemDefinition"/> data asset; empty checks <see cref="GrantedTag"/>.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;

    public Tag GrantedTag { get; set; }

    public ContextEntity Subject { get; set; }

    public override string Describe()
    {
        if (Item.Length > 0)
        {
            return $"Wearing {InventoryContext.Name(Item)}";
        }

        return $"Wearing {GrantedTag}";
    }

    protected override bool Evaluate(ConditionContext context)
    {
        IEntityView? subject = context.GetEntity(Subject);
        if (subject is null || !subject.TryGet(out ActiveEquipment equipment))
        {
            return false;
        }

        if (Item.Length == 0)
        {
            List<Tag> granted = EquipmentSystem.GetGrantedTags(equipment);
            return granted.Exists(tag => tag.MatchesTag(GrantedTag));
        }

        ItemDefinition? item = InventoryContext.LoadItem(context, Item);
        return item is not null && equipment.Items.Exists(worn => worn.Item == item);
    }
}
