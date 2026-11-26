using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Draws the meshes the view sees into the scene's depth and the <see cref="GBuffer"/>, unlit, cutting away what the cutaway
/// does, for screen shadows and lighting to read. Each draw reads its record in the frame's data buffer by its number. Materials
/// with a master are drawn by its geometry pass, made the first time one is drawn and again when the content build changes it or
/// its file, both sides of the master's surfaces when it is two-sided, a master at a time after those without.
/// </summary>
internal sealed class GeometryPass : IRenderPass
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly ShaderBytecode _vertexShader;
    private readonly IPipeline _pipeline;
    private readonly Dictionary<string, IPipeline> _masters = new(ContentManager.PathComparer);
    private readonly Dictionary<string, string> _masterShaders = new(ContentManager.PathComparer);
    private readonly List<string> _drawnMasters = [];
    private readonly ColorTarget[] _targets = new ColorTarget[GBuffer.Formats.Count];

    public GeometryPass(IGraphicsDevice device, ShaderLibrary shaders, IContentManager content)
    {
        _device = device;
        _content = content;
        _vertexShader = shaders.Load("Scene", "VertexMain");
        ShaderBytecode pixelShader = shaders.Load("Scene", "GeometryMain");
        _pipeline = CreatePipeline(pixelShader, "Geometry");
        _content.Changed += HandleChanged;
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ICommandList commands = context.Commands;
        IReadOnlyList<ITexture> gBuffer = context.GBuffer.Targets;
        for (int index = 0; index < _targets.Length; index++)
        {
            ITexture target = gBuffer[index];
            commands.Barrier(target, ResourceState.RenderTarget);
            _targets[index] = new ColorTarget(target, LoadAction.Clear);
        }

        ITexture depthTarget = context.Targets.Depth;
        commands.Barrier(depthTarget, ResourceState.DepthWrite);
        DepthTarget clearDepth = new(depthTarget, LoadAction.Clear);
        commands.BeginRendering(_targets, clearDepth);
        commands.SetViewport(new Viewport(0, 0, context.RenderWidth, context.RenderHeight));
        commands.SetScissor(0, 0, context.RenderWidth, context.RenderHeight);
        commands.SetPipeline(_pipeline);
        _drawnMasters.Clear();
        DrawMeshes(context, null);
        foreach (string master in _drawnMasters)
        {
            IPipeline pipeline = GetMasterPipeline(master);
            commands.SetPipeline(pipeline);
            DrawMeshes(context, master);
        }

        commands.EndRendering();
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
        _pipeline.Dispose();
        foreach (IPipeline pipeline in _masters.Values)
        {
            pipeline.Dispose();
        }

        _masters.Clear();
    }

    private IPipeline CreatePipeline(ShaderBytecode pixelShader, string name, CullMode cull = CullMode.Back)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = _vertexShader,
            PixelShader = pixelShader,
            CullMode = cull,
            ColorFormats = GBuffer.Formats,
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = name,
        };
        return _device.CreateGraphicsPipeline(description);
    }

    private IPipeline GetMasterPipeline(string master)
    {
        if (_masters.TryGetValue(master, out IPipeline? made))
        {
            return made;
        }

        string shaderPath = MasterFile.GetGeometryPath(master);
        string shaderFullPath = _content.GetFullPath(shaderPath);
        byte[] code = File.ReadAllBytes(shaderFullPath);
        ShaderBytecode pixelShader = new(code, MasterFile.GeometryEntryPoint);
        MasterFile file = _content.Load(master, RenderingJsonContext.Default.MasterFile);
        CullMode cull = CullMode.Back;
        if (file.TwoSided)
        {
            cull = CullMode.None;
        }

        IPipeline pipeline = CreatePipeline(pixelShader, $"Geometry {master}", cull);
        _masters.Add(master, pipeline);
        _masterShaders[shaderFullPath] = master;
        string masterFullPath = _content.GetFullPath(master);
        _masterShaders[masterFullPath] = master;
        return pipeline;
    }

    // The device destroys a replaced pipeline once the GPU is done with it.
    private void HandleChanged(string fullPath)
    {
        if (_masterShaders.TryGetValue(fullPath, out string? master) && _masters.Remove(master, out IPipeline? pipeline))
        {
            pipeline.Dispose();
        }
    }

    /// <summary>Draws the meshes in view whose material has the master, or none; without one, notes the masters it passes over.</summary>
    private void DrawMeshes(RenderContext context, string? master)
    {
        ICommandList commands = context.Commands;
        for (int index = 0; index < context.Draws.Count; index++)
        {
            MeshDraw draw = context.Draws[index];
            if (!draw.InView)
            {
                continue;
            }

            string? drawMaster = draw.Material.Master;
            if (!ContentManager.PathComparer.Equals(drawMaster, master))
            {
                if (master is null && !_drawnMasters.Contains(drawMaster!, ContentManager.PathComparer))
                {
                    _drawnMasters.Add(drawMaster!);
                }

                continue;
            }

            GpuMeshPart part = draw.Part;
            SceneConstants constants = new(context.FrameBuffer, (uint)index);
            commands.SetIndexBuffer(part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(part.IndexCount);
        }
    }
}
