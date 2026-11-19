namespace FlickerVoid.Engine.Graphics.Buffers;

/// <summary>Size, use and memory of a buffer.</summary>
public readonly record struct BufferDescription(long Size, BufferUsage Usage, MemoryLocation Memory = MemoryLocation.Gpu)
{
    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }
}
