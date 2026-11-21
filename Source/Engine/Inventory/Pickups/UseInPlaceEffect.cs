using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>The instigator uses the target pickup's item where it lies; an interaction offer's effect.</summary>
public sealed class UseInPlaceEffect : Effect
{
    public override string Describe() => "Use";

    public override void Apply(ConditionContext context)
    {
        PickupSystem? pickups = context.GetService<PickupSystem>();
        if (pickups is null || context.Instigator is not WorldEntityView user || context.Target is not WorldEntityView pickup)
        {
            return;
        }

        pickups.UseInPlace(user.Entity, pickup.Entity);
    }
}
