namespace FlickerVoid.Engine.Graphics.Resources;

/// <summary>Hands out bindless slots from 1 to capacity - 1, reusing freed ones; slot 0 is the placeholder.</summary>
public sealed class BindlessIndexAllocator(int capacity)
{
    private readonly Stack<uint> _free = new();
    private uint _next = 1;

    public int Capacity { get; } = capacity;

    public uint Allocate()
    {
        if (_free.TryPop(out uint index))
        {
            return index;
        }

        if (_next >= Capacity)
        {
            throw new InvalidOperationException($"All {Capacity} bindless slots are in use.");
        }

        uint allocated = _next;
        _next++;
        return allocated;
    }

    public void Free(uint index)
    {
        if (index == BindlessIndex.None)
        {
            return;
        }

        _free.Push(index);
    }
}

