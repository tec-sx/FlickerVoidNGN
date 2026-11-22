using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Editor.Materials;

/// <summary>
/// Renders materials on a grey sphere or cube with the game's own renderer, on a device of its own, and reads the image
/// back, so the material editor can show it without sharing textures with Avalonia. It reads the editor's content, so the
/// material's textures are found, and writes the preview meshes and the material being shown into a hidden folder there.
/// </summary>
public sealed class MaterialPreview : IMaterialPreview, IDisposable
{
    private const string FolderName = ".material-preview";
    private const string MaterialPath = FolderName + "/Preview.fvmat";
    private const string SpherePath = FolderName + "/Sphere.glb";
    private const string CubePath = FolderName + "/Cube.glb";
    private const PixelFormat Format = PixelFormat.Rgba8UNormSrgb;
    private const float CameraHeight = 1.2f;
    private const float CameraDistance = 3.2f;

    private readonly string _folder;
    private readonly ContentManager _content;
    private readonly Game _game;
    private readonly Entity _mesh;
    private readonly VulkanGraphicsDevice _device;
    private readonly SceneRenderer _renderer;
    private readonly ICommandList _commands;
    private ITexture? _target;
    private IBuffer? _readback;

    /// <summary>Throws <see cref="GraphicsUnavailableException"/> without a Vulkan GPU.</summary>
    public MaterialPreview(ILoggerFactory loggerFactory, string contentRoot)
    {
        _content = new ContentManager(contentRoot);
        _folder = _content.GetFullPath(FolderName);
        Directory.CreateDirectory(_folder);
        ModelData sphere = PreviewMeshes.CreateSphere(radius: 0.75f, rings: 32, segments: 48);
        WriteModel(Path.ChangeExtension(SpherePath, ".fvmodel"), sphere);
        ModelData cube = PreviewMeshes.CreateCube(size: 0.95f);
        WriteModel(Path.ChangeExtension(CubePath, ".fvmodel"), cube);
        _game = new Game(loggerFactory);
        _game.AddRendering();
        CreateCamera(_game.World);
        _mesh = _game.World.Create(new Transform(), new MeshRenderer { Mesh = SpherePath });
        try
        {
            _device = new VulkanGraphicsDevice(new GraphicsDeviceOptions(), loggerFactory);
        }
        catch
        {
            _game.Dispose();
            Directory.Delete(_folder, recursive: true);
            throw;
        }

        _renderer = new SceneRenderer(_device, _content, Format) { Mood = CreateMood() };
        _commands = _device.CreateCommandList();
    }

    public byte[] Render(MaterialFile material, PreviewShape shape, float yaw, int size)
    {
        WriteMaterial(material);
        string mesh = SpherePath;
        if (shape == PreviewShape.Cube)
        {
            mesh = CubePath;
        }

        World world = _game.World;
        Transform turned = new() { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw) };
        MeshRenderer renderer = new() { Mesh = mesh, Materials = [MaterialPath] };
        world.Set(_mesh, turned);
        world.Set(_mesh, renderer);
        Resize(size);
        ITexture target = _target!;
        IBuffer readback = _readback!;
        _commands.Begin();
        _renderer.Render(_commands, world, target);
        _commands.Barrier(target, ResourceState.CopySource);
        _commands.CopyTextureToBuffer(target, readback, 0);
        _commands.End();
        _device.Submit(_commands);
        _device.WaitIdle();
        return ReadPixels(readback, size);
    }

    public void Dispose()
    {
        _device.WaitIdle();
        _commands.Dispose();
        _readback?.Dispose();
        _target?.Dispose();
        _renderer.Dispose();
        _device.Dispose();
        _game.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Looks down at the shape a little from in front, framing it with a margin.</summary>
    private static void CreateCamera(World world)
    {
        float pitch = -MathF.Atan2(CameraHeight, CameraDistance);
        Transform transform = new()
        {
            Position = new Vector3(0, CameraHeight, CameraDistance),
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch),
        };
        Camera camera = new() { Projection = CameraProjection.Perspective, FieldOfView = 32, NearPlane = 0.1f, FarPlane = 20 };
        world.Create(transform, camera);
    }

    /// <summary>A sun from the upper left in front and soft ambient light, with nothing screen-based to darken a lone shape.</summary>
    private static LightingMood CreateMood()
    {
        Vector3 sun = Vector3.Normalize(new Vector3(0.6f, -0.5f, -0.7f));
        return new LightingMood { SunDirection = sun, Ambient = 0.3f, AmbientOcclusion = 0, ContactShadowLength = 0 };
    }

    private static byte[] ReadPixels(IBuffer readback, int size)
    {
        int pitch = Format.GetCopyRowPitch(size);
        int rowBytes = size * 4;
        byte[] pixels = new byte[rowBytes * size];
        for (int y = 0; y < size; y++)
        {
            Span<byte> row = pixels.AsSpan(y * rowBytes, rowBytes);
            readback.Read(row, (long)y * pitch);
        }

        return pixels;
    }

    private void WriteModel(string path, ModelData model)
    {
        string fullPath = _content.GetFullPath(path);
        using FileStream stream = File.Create(fullPath);
        ModelFile.Write(model, stream);
    }

    private void WriteMaterial(MaterialFile material)
    {
        string fullPath = _content.GetFullPath(MaterialPath);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(material, RenderingJsonContext.Default.MaterialFile);
        File.WriteAllBytes(fullPath, json);
        _content.NotifyChanged(MaterialPath);
    }

    private void Resize(int size)
    {
        if (_target is not null && _target.Description.Width == size)
        {
            return;
        }

        _readback?.Dispose();
        _target?.Dispose();
        TextureDescription target = new(size, size, Format, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = "Material preview" };
        _target = _device.CreateTexture(target);
        int pitch = Format.GetCopyRowPitch(size);
        BufferDescription readback = new(pitch * size, BufferUsage.CopyDestination, MemoryLocation.Readback);
        _readback = _device.CreateBuffer(readback);
    }
}
