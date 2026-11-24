using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Inventories;

public sealed partial class InventorySystem
{
    /// <summary>A stack of the item can be used: see <see cref="CanUseStack"/>.</summary>
    public bool CanUseItem(Entity owner, ItemDefinition item)
    {
        Guid id = FindStackToUse(owner, item);
        return CanUseStack(owner, id);
    }

    /// <summary>Uses the item from one of its stacks: a partly used unit first, else a plain stack. See <see cref="UseStack"/>.</summary>
    public bool UseItem(Entity owner, ItemDefinition item)
    {
        Guid id = FindStackToUse(owner, item);
        return UseStack(owner, id);
    }

    /// <summary>The stack's item is usable and its use conditions hold for the owner.</summary>
    public bool CanUseStack(Entity owner, Guid id)
    {
        ItemStack? stack = FindStack(owner, id);
        UsableFragment? usable = stack?.Item.FindFragment<UsableFragment>();
        if (stack is null || usable is null)
        {
            return false;
        }

        // A unit this use would use up must be able to leave the inventory.
        ItemStack spent = stack.Copy();
        if (spent.SpendUse() && !CanRemoveFromStack(owner, stack, 1))
        {
            return false;
        }

        ConditionContext context = CreateContext(owner);
        return usable.UseConditions.Evaluate(context);
    }

    /// <summary>
    /// Applies the item's usable effects to the owner. With charges one use is spent (a partly used unit becomes its own stack) and the
    /// unit goes after its last use; without, one unit is consumed when the item is consumed on use.
    /// </summary>
    public bool UseStack(Entity owner, Guid id)
    {
        if (!CanUseStack(owner, id))
        {
            return false;
        }

        ItemDefinition item = FindStack(owner, id)!.Item;
        UsableFragment usable = item.FindFragment<UsableFragment>()!;
        ConditionContext context = CreateContext(owner);
        usable.Effects.Apply(context);

        // Effects may have changed the inventory, so the stack is looked up again.
        SpendUse(owner, id);
        _events.Send(new ItemUsedEvent(owner, item));
        return true;
    }

    /// <summary>Uses left on the stack's next unit; 0 when there is no such stack.</summary>
    public int GetCharges(Entity owner, Guid id)
    {
        ItemStack? stack = FindStack(owner, id);
        if (stack is null)
        {
            return 0;
        }

        return stack.UsesLeft;
    }

    /// <summary>A partly used unit first, then plain stacks before customized ones, and worn ones last.</summary>
    private Guid FindStackToUse(Entity owner, ItemDefinition item)
    {
        ItemStack? best = null;
        int bestPriority = int.MaxValue;
        foreach (ItemStack stack in GetStacks(owner))
        {
            if (stack.Item != item)
            {
                continue;
            }

            int priority = 2;
            if (stack.FindData<ChargesLeft>() is not null)
            {
                priority = 0;
            }
            else if (stack.IsPlain)
            {
                priority = 1;
            }

            if (IsWorn(owner, stack.Id))
            {
                priority += 3;
            }

            if (priority < bestPriority)
            {
                best = stack;
                bestPriority = priority;
            }
        }

        if (best is null)
        {
            return Guid.Empty;
        }

        return best.Id;
    }

    private void SpendUse(Entity owner, Guid id)
    {
        ItemStack? stack = FindStack(owner, id);
        if (stack is null)
        {
            return;
        }

        bool charges = stack.Item.FindFragment<ChargesFragment>() is not null;
        ItemStack spent = stack.Copy();
        if (spent.SpendUse())
        {
            RemoveStack(owner, id, 1);
        }
        else if (charges && spent.Quantity > 1)
        {
            // The partly used unit keeps the stack's id, so whoever holds the id goes on draining it; the full units left become a stack of their own.
            ItemStack full = spent.Copy();
            full.RemoveData<ChargesLeft>();
            full.Id = Guid.NewGuid();
            full.Quantity = spent.Quantity - 1;
            stack.Data = spent.Data;
            stack.Quantity = 1;
            List<ItemStack> stacks = FindStacks(owner)!;
            stacks.Add(full);
            _events.Send(new StackChangedEvent(owner, id));
            _events.Send(new StackChangedEvent(owner, full.Id));
            _events.Send(new CapacityChangedEvent(owner));
            return;
        }

        // The unit's charges left changed, or were cleared from the full units left.
        ItemStack? rest = FindStack(owner, id);
        if (charges && rest is not null)
        {
            rest.Data = spent.Data;
            _events.Send(new StackChangedEvent(owner, id));
        }
    }
}
