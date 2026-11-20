namespace FlickerVoid.Engine.Graphics.Buffers;

/// <summary>Where a buffer lives: GPU memory, or CPU-visible memory the CPU writes to or reads from.</summary>
public enum MemoryLocation : byte
{
    Gpu,

    /// <summary>The CPU writes it and the GPU reads it, for staging and per-frame data.</summary>
    Upload,

    /// <summary>The GPU writes it with copies and the CPU reads it.</summary>
    Readback,
}

