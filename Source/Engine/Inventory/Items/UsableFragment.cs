using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Inventory.Items;

/// <summary>Can be used from the inventory, with its owner as instigator and target.</summary>
public sealed class UsableFragment : ItemFragment
{
    public ConditionSet UseConditions { get; set; } = new();

    public EffectList Effects { get; set; } = new();

    public bool ConsumeOnUse { get; set; } = true;
}
