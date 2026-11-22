using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>What a save keeps of a stack: its id, its item's file, its quantity and its instance data.</summary>
public sealed class SavedStack
{
    public Guid Id { get; set; }

    public string Item { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public List<ItemInstanceData> Data { get; set; } = [];

    /// <summary>The stack as saved; null when its item wasn't loaded from a file, so a save can't name it.</summary>
    public static SavedStack? From(ItemStack stack, DataAssets data)
    {
        if (!data.TryGetPath(stack.Item, out string path))
        {
            return null;
        }

        return new SavedStack { Id = stack.Id, Item = path, Quantity = stack.Quantity, Data = [.. stack.Data] };
    }

    /// <summary>The stack again; null when its item's file can't be read any more.</summary>
    public ItemStack? ToStack(DataAssets data)
    {
        ItemDefinition item;
        try
        {
            item = data.Load<ItemDefinition>(Item);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }

        return new ItemStack(item, Quantity) { Id = Id, Data = [.. Data] };
    }
}
