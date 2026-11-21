namespace FlickerVoid.Engine.Inventory.Items;

/// <summary>The base price for trading.</summary>
public sealed class ValueFragment : ItemFragment
{
    /// <summary>Price of one unit before the merchant's multipliers.</summary>
    public int BasePrice { get; set; } = 1;

    /// <summary>Merchants buy it.</summary>
    public bool Sellable { get; set; } = true;
}
