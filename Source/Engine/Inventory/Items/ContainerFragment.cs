namespace FlickerVoid.Engine.Inventory.Items;

/// <summary>A bag or backpack: adds cells and weight allowance while worn.</summary>
public sealed class ContainerFragment : ItemFragment
{
    public int Cells { get; set; } = 12;

    /// <summary>Extra kilograms.</summary>
    public float WeightBonus { get; set; } = 5;
}
