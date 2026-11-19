using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Shaders;

using Hexa.NET.ImGui;

namespace FlickerVoid.Engine.Debug;

/// <summary>
/// Draws Dear ImGui's draw data onto a texture with the engine's graphics layer: its textures (the font atlas) are kept as
/// GPU textures, and each frame's vertices and indices go to buffers of that frame's slot, grown when they run out.
/// </summary>
internal sealed unsafe class DebugUiRenderer : IDisposable
{
    public const int FramesInFlight = 3;

    private const int VertexSize = 20;
    private const int IndexSize = 2;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _pipeline;
    private readonly ISampler _sampler;
    private readonly bool _linearOutput;
    private readonly IBuffer?[] _vertices = new IBuffer?[FramesInFlight];
    private readonly IBuffer?[] _indices = new IBuffer?[FramesInFlight];
    private readonly Dictionary<int, ITexture> _textures = [];
    private int _slot;

    public DebugUiRenderer(IGraphicsDevice device, IShaderCompiler compiler, PixelFormat targetFormat)
    {
        _device = device;
        _linearOutput = targetFormat is PixelFormat.Bgra8UNormSrgb or PixelFormat.Rgba8UNormSrgb;
        GraphicsPipelineDescription description = new()
        {
            VertexShader = EmbeddedShaders.Compile(compiler, "DebugUi.hlsl", "VertexMain", ShaderStage.Vertex),
            PixelShader = EmbeddedShaders.Compile(compiler, "DebugUi.hlsl", "PixelMain", ShaderStage.Pixel),
            ColorFormats = [targetFormat],
            CullMode = CullMode.None,
            Blend = BlendMode.AlphaBlend,
            Name = "Debug UI",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
        _sampler = device.CreateSampler(new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp));
    }

    /// <summary>Draws the frame's draw data over the target, which keeps what it shows; leaves it in the RenderTarget state.</summary>
    public void Draw(ICommandList commands, ITexture target, ImDrawDataPtr drawData)
    {
        UpdateTextures(drawData);
        if (drawData.CmdListsCount == 0 || drawData.TotalVtxCount == 0)
        {
            return;
        }

        _slot = (_slot + 1) % FramesInFlight;
        IBuffer vertices = Upload(_vertices, VertexSize, drawData.TotalVtxCount, BufferUsage.ShaderRead, "Debug UI vertices");
        IBuffer indices = Upload(_indices, IndexSize, drawData.TotalIdxCount, BufferUsage.Index, "Debug UI indices");
        int vertexBase = 0;
        int indexBase = 0;
        for (int list = 0; list < drawData.CmdListsCount; list++)
        {
            ImDrawListPtr commandList = drawData.CmdLists[list];
            vertices.Write(new ReadOnlySpan<byte>(commandList.VtxBuffer.Data, commandList.VtxBuffer.Size * VertexSize), (long)vertexBase * VertexSize);
            indices.Write(new ReadOnlySpan<byte>(commandList.IdxBuffer.Data, commandList.IdxBuffer.Size * IndexSize), (long)indexBase * IndexSize);
            vertexBase += commandList.VtxBuffer.Size;
            indexBase += commandList.IdxBuffer.Size;
        }

        int width = target.Description.Width;
        int height = target.Description.Height;
        commands.Barrier(target, ResourceState.RenderTarget);
        commands.BeginRendering([new ColorTarget(target)]);
        commands.SetViewport(new Viewport(0, 0, width, height));
        commands.SetPipeline(_pipeline);
        commands.SetIndexBuffer(indices, IndexFormat.UInt16);

        // Pixels run down from the top left, but this engine's clip space is y-up.
        Vector2 scale = new(2f / width, -2f / height);
        Vector2 translate = new(-1, 1);
        uint linear = _linearOutput ? 1u : 0u;
        vertexBase = 0;
        indexBase = 0;
        for (int list = 0; list < drawData.CmdListsCount; list++)
        {
            ImDrawListPtr commandList = drawData.CmdLists[list];
            for (int index = 0; index < commandList.CmdBuffer.Size; index++)
            {
                ImDrawCmd command = commandList.CmdBuffer[index];
                if (command.UserCallback != null || command.ElemCount == 0)
                {
                    continue;
                }

                int left = Math.Clamp((int)command.ClipRect.X, 0, width);
                int top = Math.Clamp((int)command.ClipRect.Y, 0, height);
                int right = Math.Clamp((int)command.ClipRect.Z, 0, width);
                int bottom = Math.Clamp((int)command.ClipRect.W, 0, height);
                if (right <= left || bottom <= top)
                {
                    continue;
                }

                int textureIndex = (int)command.GetTexID().Handle;
                DebugUiConstants constants = new(scale, translate, vertices.BindlessIndex, _textures[textureIndex].BindlessIndex, _sampler.BindlessIndex, linear);
                commands.SetScissor(left, top, right - left, bottom - top);
                commands.PushConstants(constants);
                commands.DrawIndexed((int)command.ElemCount, 1, indexBase + (int)command.IdxOffset, vertexBase + (int)command.VtxOffset);
            }

            vertexBase += commandList.VtxBuffer.Size;
            indexBase += commandList.IdxBuffer.Size;
        }

        commands.EndRendering();
    }

    public void Dispose()
    {
        foreach (ITexture texture in _textures.Values)
        {
            texture.Dispose();
        }

        foreach (IBuffer? buffer in _vertices)
        {
            buffer?.Dispose();
        }

        foreach (IBuffer? buffer in _indices)
        {
            buffer?.Dispose();
        }

        _sampler.Dispose();
        _pipeline.Dispose();
    }

    // ImGui asks for textures to be created, updated (the whole texture is uploaded again) and destroyed through its draw data.
    private void UpdateTextures(ImDrawDataPtr drawData)
    {
        for (int index = 0; index < drawData.Textures.Size; index++)
        {
            ImTextureDataPtr data = drawData.Textures[index];
            if (data.Status == ImTextureStatus.WantCreate || data.Status == ImTextureStatus.WantUpdates)
            {
                Upload(data);
            }
            else if (data.Status == ImTextureStatus.WantDestroy && data.UnusedFrames >= FramesInFlight)
            {
                Destroy(data);
            }
        }
    }

    private void Upload(ImTextureDataPtr data)
    {
        int id = (int)data.GetTexID().Handle;
        if (id == 0 || !_textures.ContainsKey(id))
        {
            id = _textures.Count + 1;
            while (_textures.ContainsKey(id))
            {
                id++;
            }
        }
        else
        {
            _textures[id].Dispose();
        }

        TextureDescription description = new(data.Width, data.Height, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead) { Name = "Debug UI texture" };
        ReadOnlySpan<byte> pixels = new(data.GetPixels(), data.Width * data.Height * 4);
        _textures[id] = _device.CreateTexture(description, pixels);
        data.SetTexID(new ImTextureID((ulong)id));
        data.SetStatus(ImTextureStatus.Ok);
    }

    private void Destroy(ImTextureDataPtr data)
    {
        int id = (int)data.GetTexID().Handle;
        if (_textures.Remove(id, out ITexture? texture))
        {
            texture.Dispose();
        }

        data.SetTexID(new ImTextureID(0));
        data.SetStatus(ImTextureStatus.Destroyed);
    }

    private IBuffer Upload(IBuffer?[] buffers, int elementSize, int count, BufferUsage usage, string name)
    {
        long size = (long)count * elementSize;
        IBuffer? buffer = buffers[_slot];
        if (buffer is not null && buffer.Description.Size >= size)
        {
            return buffer;
        }

        buffer?.Dispose();
        long capacity = (long)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)size);
        BufferDescription description = new(capacity, usage, MemoryLocation.Upload) { Name = $"{name} {_slot}" };
        buffer = _device.CreateBuffer(description);
        buffers[_slot] = buffer;
        return buffer;
    }
}
