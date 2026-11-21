using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>The instigator picks up the target pickup, as much as fits; an interaction offer's effect.</summary>
public sealed class PickUpEffect : Effect
{
    public override string Describe() => "Pick up";

    public override void Apply(ConditionContext context)
    {
        PickupSystem? pickups = context.GetService<PickupSystem>();
        if (pickups is null || context.Instigator is not WorldEntityView picker || context.Target is not WorldEntityView pickup)
        {
            return;
        }

        pickups.TryPickUp(picker.Entity, pickup.Entity);
    }
}
