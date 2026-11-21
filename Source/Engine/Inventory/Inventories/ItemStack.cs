using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>Some units of an item, optionally carrying instance data; stacks with instance data never merge with others.</summary>
public sealed class ItemStack
{
    public ItemStack(ItemDefinition item, int quantity = 1)
    {
        Item = item;
        Quantity = quantity;
    }

    /// <summary>Assigned when the stack enters an inventory; equipment and the interface name the stack by it.</summary>
    public Guid Id { get; set; }

    public ItemDefinition Item { get; }

    public int Quantity { get; set; }

    public List<ItemInstanceData> Data { get; set; } = [];

    public bool IsPlain => Data.Count == 0;

    /// <summary>Uses left on the next unit: its charges left, else the item's charges, else 1 for a usable item and 0 otherwise.</summary>
    public int UsesLeft
    {
        get
        {
            ChargesLeft? left = FindData<ChargesLeft>();
            if (left is not null && left.Remaining > 0)
            {
                return left.Remaining;
            }

            ChargesFragment? charges = Item.FindFragment<ChargesFragment>();
            if (charges is not null)
            {
                return Math.Max(1, charges.Charges);
            }

            if (Item.IsUsable)
            {
                return 1;
            }

            return 0;
        }
    }

    public T? FindData<T>()
        where T : ItemInstanceData
    {
        return Data.OfType<T>().FirstOrDefault();
    }

    /// <summary>Replaces the data of the same type, or adds it.</summary>
    public void SetData<T>(T data)
        where T : ItemInstanceData
    {
        RemoveData<T>();
        Data.Add(data);
    }

    public bool RemoveData<T>()
        where T : ItemInstanceData
    {
        return Data.RemoveAll(static data => data is T) > 0;
    }

    /// <summary>
    /// One use of the next unit: a charge, kept as its charges left, or the unit itself after its last charge or when the item is
    /// consumed on use. A used up unit lowers the quantity and returns true.
    /// </summary>
    public bool SpendUse()
    {
        UsableFragment? usable = Item.FindFragment<UsableFragment>();
        if (usable is null || Quantity <= 0)
        {
            return false;
        }

        bool usedUp = usable.ConsumeOnUse;
        if (Item.FindFragment<ChargesFragment>() is not null)
        {
            int left = UsesLeft - 1;
            usedUp = left <= 0;
            if (usedUp)
            {
                RemoveData<ChargesLeft>();
            }
            else
            {
                SetData(new ChargesLeft(left));
            }
        }

        if (usedUp)
        {
            Quantity--;
        }

        return usedUp;
    }

    /// <summary>A copy with its own data list and the same id.</summary>
    public ItemStack Copy()
    {
        return new ItemStack(Item, Quantity) { Id = Id, Data = [.. Data] };
    }
}
