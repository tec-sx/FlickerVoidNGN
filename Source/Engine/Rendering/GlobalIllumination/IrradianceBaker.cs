using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.GlobalIllumination;

/// <summary>
/// Bakes an irradiance volume's probes with the rays live probes are traced with (<see cref="ProbePass"/>), but
/// <see cref="ProbePass.BakeRays"/> of them a probe: each bounce is a frame of its own scene renderer, whose rays find the scene
/// lit directly and by the probes of the bounce before. Needs a GPU with ray queries.
/// </summary>
public sealed class IrradianceBaker : IDisposable
{
    // The frames' color is never read; the rays write the probes.
    private const PixelFormat TargetFormat = PixelFormat.Rgba32Float;

    private readonly IGraphicsDevice _device;
    private readonly ITexture _color;
    private readonly ICommandList _commands;

    public IrradianceBaker(IGraphicsDevice device, IContentManager content)
    {
        if (!device.Features.HasFlag(GraphicsFeatures.RayQueries))
        {
            throw new GraphicsUnavailableException("Baking probes traces rays, and this GPU has no ray queries.");
        }

        _device = device;
        Renderer = new SceneRenderer(device, content, TargetFormat) { RayTracing = true };
        TextureDescription colorDescription = new(1, 1, TargetFormat, TextureUsage.RenderTarget) { Name = "Probe bake" };
        _color = device.CreateTexture(colorDescription);
        _commands = device.CreateCommandList();
    }

    /// <summary>The renderer whose frames trace the probes; give it the game's mood. Baking ignores its post-processing.</summary>
    public SceneRenderer Renderer { get; }

    /// <summary>Bakes the probes of a volume centred on the position, a frame a bounce; waits for the GPU after each.</summary>
    public IrradianceProbes Bake(World world, in IrradianceVolume volume, Vector3 center, int bounces = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bounces, 1);
        IrradianceProbes probes = new(volume.ProbesX, volume.ProbesY, volume.ProbesZ);
        BufferDescription readbackDescription = new(probes.ByteCount, BufferUsage.CopyDestination, MemoryLocation.Readback) { Name = "Probe readback" };
        using IBuffer readback = _device.CreateBuffer(readbackDescription);
        Camera camera = new() { Projection = CameraProjection.Perspective, FieldOfView = 90, NearPlane = 0.05f, FarPlane = 1000 };
        Transform transform = new() { Position = center };
        LightingMood mood = Renderer.Mood;
        Renderer.Mood = mood.WithoutPost();
        ProbePass pass = Renderer.ProbePass;
        pass.Bake = new RenderProbeVolume(volume, center);
        try
        {
            for (int bounce = 1; bounce <= bounces; bounce++)
            {
                _commands.Begin();
                Renderer.Render(_commands, world, _color, camera, transform);
                if (bounce == bounces)
                {
                    pass.CopyProbes(_commands, readback);
                }

                _commands.End();
                _device.Submit(_commands);
                _device.WaitIdle();
            }
        }
        finally
        {
            pass.Bake = null;
            Renderer.Mood = mood;
        }

        byte[] bytes = new byte[probes.ByteCount];
        readback.Read(bytes, 0);
        probes.ReadFrom(bytes);
        return probes;
    }

    public void Dispose()
    {
        _device.WaitIdle();
        _commands.Dispose();
        _color.Dispose();
        Renderer.Dispose();
    }
}
