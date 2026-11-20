using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Editor.Core.Overlays;

/// <summary>
/// Draws the overlay over the finished image, in front of everything: its triangles, then its icons. Add it last. Each frame's
/// triangles and icons go to buffers of that frame's slot, grown when they don't fit.
/// </summary>
public sealed class OverlayPass : IRenderPass
{
    private const int FramesInFlight = SceneRenderer.MaxFramesInFlight;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _shapes;
    private readonly IPipeline _iconPipeline;
    private readonly ISampler _sampler;
    private readonly Dictionary<EditorIcon, ITexture> _iconTextures = [];
    private readonly IBuffer?[] _vertexBuffers = new IBuffer?[FramesInFlight];
    private readonly IBuffer?[] _iconBuffers = new IBuffer?[FramesInFlight];
    private readonly List<OverlayIconData> _iconData = [];
    private int _slot;

    /// <summary>Creates the pass with the icons the content build put in the folder, as &lt;icon&gt;.dds.</summary>
    public OverlayPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat colorFormat, string iconFolder)
    {
        _device = device;
        GraphicsPipelineDescription shapes = new()
        {
            VertexShader = shaders.Load("Overlay", "VertexMain"),
            PixelShader = shaders.Load("Overlay", "PixelMain"),
            ColorFormats = [colorFormat],
            CullMode = CullMode.None,
            Blend = BlendMode.AlphaBlend,
            Name = "Overlay",
        };
        _shapes = device.CreateGraphicsPipeline(shapes);
        GraphicsPipelineDescription icons = shapes with
        {
            VertexShader = shaders.Load("Icon", "VertexMain"),
            PixelShader = shaders.Load("Icon", "PixelMain"),
            Name = "Overlay icons",
        };
        _iconPipeline = device.CreateGraphicsPipeline(icons);
        SamplerDescription sampler = new(SamplerFilter.Linear, SamplerAddressMode.Clamp);
        _sampler = device.CreateSampler(sampler);
        foreach (EditorIcon icon in Enum.GetValues<EditorIcon>())
        {
            _iconTextures[icon] = LoadIcon(iconFolder, icon);
        }
    }

    /// <summary>What the next frames draw; fill it before rendering.</summary>
    public OverlayBuilder Overlay { get; } = new();

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ReadOnlySpan<OverlayVertex> vertices = Overlay.Vertices;
        ReadOnlySpan<OverlayIcon> icons = Overlay.Icons;
        if (vertices.Length == 0 && icons.Length == 0)
        {
            return;
        }

        _slot = (_slot + 1) % FramesInFlight;
        ICommandList commands = context.Commands;
        commands.Barrier(context.ColorTarget, ResourceState.RenderTarget);
        ColorTarget color = new(context.ColorTarget, LoadAction.Load);
        commands.BeginRendering([color]);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        Matrix4x4 viewProjection = context.View.ViewProjection;
        if (vertices.Length > 0)
        {
            ReadOnlySpan<byte> vertexBytes = MemoryMarshal.AsBytes(vertices);
            IBuffer buffer = Upload(_vertexBuffers, vertexBytes, "Overlay");
            commands.SetPipeline(_shapes);
            OverlayConstants constants = new(viewProjection, buffer.BindlessIndex);
            commands.PushConstants(constants);
            commands.Draw(vertices.Length);
        }

        if (icons.Length > 0)
        {
            ReadOnlySpan<OverlayIconData> data = BuildIconData(icons);
            ReadOnlySpan<byte> iconBytes = MemoryMarshal.AsBytes(data);
            IBuffer buffer = Upload(_iconBuffers, iconBytes, "Overlay icons");
            commands.SetPipeline(_iconPipeline);
            Vector2 targetSize = new(context.Width, context.Height);
            IconConstants constants = new(viewProjection, buffer.BindlessIndex, _sampler.BindlessIndex, targetSize);
            commands.PushConstants(constants);
            commands.Draw(icons.Length * 6);
        }

        commands.EndRendering();
    }

    public void Dispose()
    {
        foreach (IBuffer? buffer in _vertexBuffers)
        {
            buffer?.Dispose();
        }

        foreach (IBuffer? buffer in _iconBuffers)
        {
            buffer?.Dispose();
        }

        foreach (ITexture texture in _iconTextures.Values)
        {
            texture.Dispose();
        }

        _sampler.Dispose();
        _iconPipeline.Dispose();
        _shapes.Dispose();
    }

    private ITexture LoadIcon(string folder, EditorIcon icon)
    {
        string path = Path.Combine(folder, $"{icon}.dds");
        DdsImage image;
        using (FileStream stream = File.OpenRead(path))
        {
            image = DdsImage.Read(stream);
        }

        TextureDescription description = image.Describe(TextureUsage.ShaderRead) with { Name = $"{icon} icon" };
        return _device.CreateTexture(description, image.Data);
    }

    private ReadOnlySpan<OverlayIconData> BuildIconData(ReadOnlySpan<OverlayIcon> icons)
    {
        _iconData.Clear();
        foreach (OverlayIcon icon in icons)
        {
            ITexture texture = _iconTextures[icon.Icon];
            _iconData.Add(new OverlayIconData(icon.Position, EditorIcons.SizePixels, icon.Color, texture.BindlessIndex));
        }

        return CollectionsMarshal.AsSpan(_iconData);
    }

    private IBuffer Upload(IBuffer?[] buffers, ReadOnlySpan<byte> bytes, string name)
    {
        IBuffer? buffer = buffers[_slot];
        if (buffer is null || buffer.Description.Size < bytes.Length)
        {
            // The device releases a disposed buffer only after the GPU finishes the work already submitted, which may still read it.
            buffer?.Dispose();
            long capacity = (long)BitOperations.RoundUpToPowerOf2((ulong)bytes.Length);
            BufferDescription description = new(capacity, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = $"{name} {_slot}" };
            buffer = _device.CreateBuffer(description);
            buffers[_slot] = buffer;
        }

        buffer.Write(bytes, 0);
        return buffer;
    }
}
