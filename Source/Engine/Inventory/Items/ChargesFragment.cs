namespace FlickerVoid.Engine.Inventory.Items;

/// <summary>Several uses per unit, such as sips from a bottle; the uses left are kept on the stack.</summary>
public sealed class ChargesFragment : ItemFragment
{
    /// <summary>Uses per unit; each applies the usable effects, and the unit is used up after the last.</summary>
    public int Charges { get; set; } = 4;
}
