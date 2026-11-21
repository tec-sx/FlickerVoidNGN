using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Ui.Surfaces;

namespace FlickerVoid.Engine.Ui.Drawing;

/// <summary>
/// Draws a surface over a target: each frame the surface's pixels go into an upload buffer and a texture of that frame's slot,
/// made again when the surface's size changes, and one triangle blends the texture over the target.
/// </summary>
internal sealed class UiRenderer : IDisposable
{
    public const int FramesInFlight = 3;

    private const PixelFormat Format = PixelFormat.Rgba8UNorm;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _pipeline;
    private readonly bool _linearOutput;
    private readonly IBuffer?[] _uploads = new IBuffer?[FramesInFlight];
    private readonly ITexture?[] _textures = new ITexture?[FramesInFlight];
    private byte[] _pixels = [];
    private int _slot;

    public UiRenderer(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat targetFormat)
    {
        _device = device;
        _linearOutput = targetFormat is PixelFormat.Bgra8UNormSrgb or PixelFormat.Rgba8UNormSrgb;
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("GameUi", "VertexMain"),
            PixelShader = shaders.Load("GameUi", "PixelMain"),
            ColorFormats = [targetFormat],
            CullMode = CullMode.None,
            Blend = BlendMode.Premultiplied,
            Name = "Game UI",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
    }

    /// <summary>Draws the surface over the target, which keeps what it shows; leaves it in the RenderTarget state.</summary>
    public void Draw(ICommandList commands, ITexture target, UiSurface surface)
    {
        _slot = (_slot + 1) % FramesInFlight;
        int rowPitch = Format.GetCopyRowPitch(surface.Width);
        int size = rowPitch * surface.Height;
        if (_pixels.Length != size)
        {
            _pixels = new byte[size];
        }

        Array.Clear(_pixels);
        surface.Render(_pixels, rowPitch);
        IBuffer upload = Upload(size);
        upload.Write(_pixels);
        ITexture texture = Texture(surface.Width, surface.Height);
        commands.Barrier(texture, ResourceState.CopyDestination);
        commands.CopyBufferToTexture(upload, 0, texture);
        commands.Barrier(texture, ResourceState.ShaderRead);

        int width = Math.Min(target.Description.Width, surface.Width);
        int height = Math.Min(target.Description.Height, surface.Height);
        commands.Barrier(target, ResourceState.RenderTarget);
        commands.BeginRendering([new ColorTarget(target)]);
        commands.SetViewport(new Viewport(0, 0, surface.Width, surface.Height));
        commands.SetScissor(0, 0, width, height);
        commands.SetPipeline(_pipeline);
        uint linear = _linearOutput ? 1u : 0u;
        commands.PushConstants(new GameUiConstants(texture.BindlessIndex, linear));
        commands.Draw(3);
        commands.EndRendering();
    }

    public void Dispose()
    {
        foreach (IBuffer? upload in _uploads)
        {
            upload?.Dispose();
        }

        foreach (ITexture? texture in _textures)
        {
            texture?.Dispose();
        }

        _pipeline.Dispose();
    }

    private IBuffer Upload(int size)
    {
        IBuffer? upload = _uploads[_slot];
        if (upload is not null && upload.Description.Size == size)
        {
            return upload;
        }

        upload?.Dispose();
        BufferDescription description = new(size, BufferUsage.CopySource, MemoryLocation.Upload) { Name = $"Game UI pixels {_slot}" };
        upload = _device.CreateBuffer(description);
        _uploads[_slot] = upload;
        return upload;
    }

    private ITexture Texture(int width, int height)
    {
        ITexture? texture = _textures[_slot];
        if (texture is not null && texture.Description.Width == width && texture.Description.Height == height)
        {
            return texture;
        }

        texture?.Dispose();
        TextureDescription description = new(width, height, Format, TextureUsage.ShaderRead | TextureUsage.CopyDestination) { Name = $"Game UI {_slot}" };
        texture = _device.CreateTexture(description);
        _textures[_slot] = texture;
        return texture;
    }
}
