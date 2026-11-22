using Arch.Core;

using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Abilities.Costs;

/// <summary>Takes an amount off the base value of the owner's attribute, which must have at least that much.</summary>
public sealed class AttributeCost : AbilityCost
{
    public Tag Attribute { get; set; }

    public float Amount { get; set; } = 1;

    public override bool CanPay(ConditionContext context)
    {
        if (!TryGetOwner(context, out AttributeSystem attributes, out Entity owner) || !attributes.HasAttribute(owner, Attribute))
        {
            return false;
        }

        return attributes.GetValue(owner, Attribute) >= Amount;
    }

    public override void Pay(ConditionContext context)
    {
        if (TryGetOwner(context, out AttributeSystem attributes, out Entity owner))
        {
            attributes.AddToBaseValue(owner, Attribute, -Amount);
        }
    }

    private static bool TryGetOwner(ConditionContext context, out AttributeSystem attributes, out Entity owner)
    {
        AttributeSystem? system = context.GetService<AttributeSystem>();
        if (system is null || context.Instigator is not WorldEntityView view)
        {
            attributes = null!;
            owner = Entity.Null;
            return false;
        }

        attributes = system;
        owner = view.Entity;
        return true;
    }
}
