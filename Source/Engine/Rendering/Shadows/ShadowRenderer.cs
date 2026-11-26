using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// Draws the depth of every mesh into the shadow atlas, once per shadow view it is in, leaving the atlas ready to sample; parts in a
/// cutout material leave out their holes.
/// </summary>
internal sealed class ShadowRenderer : IDisposable
{
    private const string ShaderName = "Shadow";

    private readonly IPipeline _solid;
    private readonly IPipeline _cutout;

    public ShadowRenderer(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _solid = CreatePipeline(device, shaders, "PixelMain", "Shadow");
        _cutout = CreatePipeline(device, shaders, "CutoutMain", "Shadow cutout");
        TextureDescription atlasDescription = new(ShadowAtlas.Size, ShadowAtlas.Size, SceneRenderer.DepthFormat, TextureUsage.DepthStencil | TextureUsage.ShaderRead)
        {
            Name = "Shadow atlas",
        };
        Atlas = device.CreateTexture(atlasDescription);
    }

    public ITexture Atlas { get; }

    /// <summary>Draws the views' depth; <paramref name="frameBuffer"/> holds the draws' records, which cutout parts read.</summary>
    public void Draw(ICommandList commands, uint frameBuffer, IReadOnlyList<ShadowView> views, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        if (views.Count > 0)
        {
            commands.Barrier(Atlas, ResourceState.DepthWrite);
            DepthTarget depth = new(Atlas, LoadAction.Clear);
            commands.BeginRendering([], depth);
            foreach (ShadowView view in views)
            {
                DrawView(commands, frameBuffer, view, draws, statistics);
            }

            commands.EndRendering();
        }

        commands.Barrier(Atlas, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        Atlas.Dispose();
        _cutout.Dispose();
        _solid.Dispose();
    }

    private static IPipeline CreatePipeline(IGraphicsDevice device, ShaderLibrary shaders, string pixelShader, string name)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load(ShaderName, "VertexMain"),
            PixelShader = shaders.Load(ShaderName, pixelShader),
            ColorFormats = [],
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: true),

            // Rooms are often single-sided, so back faces cast shadows too.
            CullMode = CullMode.None,
            Name = name,
        };
        return device.CreateGraphicsPipeline(description);
    }

    private void DrawView(ICommandList commands, uint frameBuffer, in ShadowView view, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        ShadowTile tile = view.Tile;
        commands.SetViewport(new Viewport(tile.X, tile.Y, tile.Size, tile.Size));
        commands.SetScissor(tile.X, tile.Y, tile.Size, tile.Size);
        Frustum frustum = new(view.ViewProjection);
        IPipeline? current = null;
        for (int index = 0; index < draws.Count; index++)
        {
            MeshDraw draw = draws[index];
            // Light passes through glass.
            if (draw.Flags.HasFlag(MeshFlags.OutlineOnly) || draw.Material.Transparent)
            {
                continue;
            }

            if (draw.Bounds is { } bounds && !frustum.Intersects(bounds))
            {
                statistics.ShadowCulled++;
                continue;
            }

            statistics.ShadowDrawn++;
            IPipeline pipeline = draw.Material.Cutout ? _cutout : _solid;
            if (!ReferenceEquals(pipeline, current))
            {
                commands.SetPipeline(pipeline);
                current = pipeline;
            }

            ShadowConstants constants = new(draw.World * view.ViewProjection, draw.Vertices.BindlessIndex, frameBuffer, (uint)index);
            commands.SetIndexBuffer(draw.Part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(draw.Part.IndexCount);
        }
    }
}

