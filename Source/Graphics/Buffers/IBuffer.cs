using FlickerVoid.Engine.Graphics.Resources;

namespace FlickerVoid.Engine.Graphics.Buffers;

/// <summary>A GPU buffer.</summary>
public interface IBuffer : IGraphicsResource
{
    BufferDescription Description { get; }

    /// <summary>Copies data into an upload buffer.</summary>
    void Write(ReadOnlySpan<byte> data, long offset = 0);

    /// <summary>Copies data out of a readback buffer; wait for the copy that filled it first.</summary>
    void Read(Span<byte> data, long offset = 0);
}

