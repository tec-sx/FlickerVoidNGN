using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>Creates the device for the GPU timer tests, skipping where it writes no timestamps, and records timed clears.</summary>
internal sealed class GpuTimerTestsBuilder : IDisposable
{
    private static readonly Vector4 Blue = new(0, 0, 1, 1);

    private readonly GraphicsDeviceTestsBuilder _device = new();

    public IGraphicsDevice Build() => _device.Build();

    public IGraphicsDevice BuildWithTimestamps()
    {
        IGraphicsDevice device = _device.Build();
        if (!device.Features.HasFlag(GraphicsFeatures.Timestamps))
        {
            string adapter = device.AdapterName;
            device.Dispose();
            Assert.Skip($"{adapter} writes no timestamps.");
        }

        return device;
    }

    /// <summary>Records a clear of the target inside a timer of the given name, submits it and waits for it.</summary>
    public void SubmitTimed(IGraphicsDevice device, ICommandList commands, ITexture target, string name)
    {
        commands.Begin();
        commands.BeginTimer(name);
        GraphicsDeviceTestsBuilder.BeginRendering(commands, target, Blue);
        commands.EndRendering();
        commands.EndTimer();
        commands.End();
        device.Submit(commands);
        device.WaitIdle();
    }

    public void Dispose() => _device.Dispose();
}
