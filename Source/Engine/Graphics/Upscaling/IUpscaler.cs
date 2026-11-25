using FlickerVoid.Engine.Graphics.Commands;

namespace FlickerVoid.Engine.Graphics.Upscaling;

/// <summary>
/// Fills an output from a smaller, jittered frame and the frames before it, as DLSS does; made by
/// <see cref="Devices.IGraphicsDevice.CreateUpscaler"/> for one mode and output size.
/// </summary>
public interface IUpscaler : IDisposable
{
    UpscalerDescription Description { get; }

    /// <summary>The width frames are drawn at for it.</summary>
    int RenderWidth { get; }

    /// <summary>The height frames are drawn at for it.</summary>
    int RenderHeight { get; }

    /// <summary>Records the upscale into the output; the inputs must be in the ShaderRead state and the output in ShaderWrite, and are left so.</summary>
    void Upscale(ICommandList commands, in UpscaleInputs inputs);
}
