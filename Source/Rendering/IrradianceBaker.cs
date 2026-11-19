using System.Numerics;
using System.Runtime.InteropServices;

using Arch.Core;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// Bakes an irradiance volume's probes: each probe renders the scene in six directions with its own scene renderer and
/// projects what it sees. The first bounce sees the scene lit directly; each further bounce sees it lit by the probes of
/// the bounce before as well.
/// </summary>
public sealed class IrradianceBaker : IDisposable
{
    /// <summary>The width and height of each captured face; first-order harmonics need no more.</summary>
    public const int FaceSize = 16;

    private const PixelFormat CaptureFormat = PixelFormat.Rgba32Float;

    private readonly IGraphicsDevice _device;
    private readonly ITexture _color;
    private readonly ITexture _depth;
    private readonly IBuffer _readback;
    private readonly ICommandList _commands;
    private readonly int _rowPitch;

    public IrradianceBaker(IGraphicsDevice device, IShaderCompiler compiler, IContentManager content)
    {
        _device = device;
        Renderer = new SceneRenderer(device, compiler, content, CaptureFormat);
        TextureDescription colorDescription = new(FaceSize, FaceSize, CaptureFormat, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = "Probe capture" };
        TextureDescription depthDescription = new(FaceSize, FaceSize, SceneRenderer.DepthFormat, TextureUsage.DepthStencil) { Name = "Probe capture depth" };
        _color = device.CreateTexture(colorDescription);
        _depth = device.CreateTexture(depthDescription);
        _rowPitch = CaptureFormat.GetCopyRowPitch(FaceSize);
        BufferDescription readbackDescription = new(_rowPitch * FaceSize, BufferUsage.CopyDestination, MemoryLocation.Readback) { Name = "Probe readback" };
        _readback = device.CreateBuffer(readbackDescription);
        _commands = device.CreateCommandList();
    }

    /// <summary>The renderer the probes see the scene through; give it the sun and ambient light the game uses.</summary>
    public SceneRenderer Renderer { get; }

    /// <summary>Bakes the probes of a volume centred on the position; waits for the GPU after every face.</summary>
    public IrradianceProbes Bake(World world, in IrradianceVolume volume, Vector3 center, int bounces = 2)
    {
        IrradianceProbes? previous = null;
        Vector4[] pixels = new Vector4[CubeCapture.Faces * FaceSize * FaceSize];
        for (int bounce = 0; bounce < bounces; bounce++)
        {
            Renderer.BakeProbes = new ProbeSource(volume, center, previous);
            IrradianceProbes baked = new(volume.ProbesX, volume.ProbesY, volume.ProbesZ);
            for (int z = 0; z < volume.ProbesZ; z++)
            {
                for (int y = 0; y < volume.ProbesY; y++)
                {
                    for (int x = 0; x < volume.ProbesX; x++)
                    {
                        Vector3 position = volume.GetProbePosition(center, x, y, z);
                        Capture(world, position, pixels);
                        int index = baked.GetIndex(x, y, z);
                        baked.Probes[index] = CubeCapture.Project(pixels, FaceSize);
                    }
                }
            }

            previous = baked;
        }

        Renderer.BakeProbes = null;
        return previous ?? new IrradianceProbes(volume.ProbesX, volume.ProbesY, volume.ProbesZ);
    }

    public void Dispose()
    {
        _device.WaitIdle();
        _commands.Dispose();
        _readback.Dispose();
        _depth.Dispose();
        _color.Dispose();
        Renderer.Dispose();
    }

    private void Capture(World world, Vector3 position, Span<Vector4> pixels)
    {
        Camera camera = new() { Projection = CameraProjection.Perspective, FieldOfView = 90, NearPlane = 0.05f, FarPlane = 1000 };
        for (int face = 0; face < CubeCapture.Faces; face++)
        {
            Transform transform = new() { Position = position, Rotation = CubeCapture.GetFaceRotation(face) };
            _commands.Begin();
            Renderer.Render(_commands, world, _color, _depth, camera, transform);
            _commands.Barrier(_color, ResourceState.CopySource);
            _commands.CopyTextureToBuffer(_color, _readback, 0);
            _commands.End();
            _device.Submit(_commands);
            _device.WaitIdle();

            Span<Vector4> facePixels = pixels.Slice(face * FaceSize * FaceSize, FaceSize * FaceSize);
            for (int row = 0; row < FaceSize; row++)
            {
                Span<Vector4> rowPixels = facePixels.Slice(row * FaceSize, FaceSize);
                Span<byte> rowBytes = MemoryMarshal.AsBytes(rowPixels);
                _readback.Read(rowBytes, (long)row * _rowPitch);
            }
        }
    }
}
