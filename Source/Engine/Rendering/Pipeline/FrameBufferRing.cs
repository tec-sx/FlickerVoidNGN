using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// One upload buffer per frame in flight, reused that many frames later and grown when the frame's data doesn't fit.
/// </summary>
internal sealed class FrameBufferRing : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IBuffer?[] _frames;
    private int _slot;

    public FrameBufferRing(IGraphicsDevice device, int framesInFlight)
    {
        _device = device;
        _frames = new IBuffer?[framesInFlight];
    }

    public int Slot => _slot;

    /// <summary>Moves to the next slot; call once per frame.</summary>
    public int Advance()
    {
        _slot = (_slot + 1) % _frames.Length;
        return _slot;
    }

    public IBuffer Get(int size)
    {
        IBuffer? frame = _frames[_slot];
        if (frame is not null && frame.Description.Size >= size)
        {
            return frame;
        }

        // The device releases a disposed buffer only after the GPU finishes the work already submitted, which may still read it.
        frame?.Dispose();
        long capacity = (long)BitOperations.RoundUpToPowerOf2((uint)size);
        BufferDescription description = new(capacity, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = $"Frame {_slot}" };
        frame = _device.CreateBuffer(description);
        _frames[_slot] = frame;
        return frame;
    }

    public void Dispose()
    {
        foreach (IBuffer? frame in _frames)
        {
            frame?.Dispose();
        }
    }
}
