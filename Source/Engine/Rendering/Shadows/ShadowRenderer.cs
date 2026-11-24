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

/// <summary>Draws the depth of every mesh into the shadow atlas, once per shadow view it is in, leaving the atlas ready to sample.</summary>
internal sealed class ShadowRenderer : IDisposable
{
    private const string ShaderName = "Shadow";

    private readonly IPipeline _pipeline;

    public ShadowRenderer(IGraphicsDevice device, ShaderLibrary shaders)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load(ShaderName, "VertexMain"),
            PixelShader = shaders.Load(ShaderName, "PixelMain"),
            ColorFormats = [],
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: true),

            // Rooms are often single-sided, so back faces cast shadows too.
            CullMode = CullMode.None,
            Name = "Shadow",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
        TextureDescription atlasDescription = new(ShadowAtlas.Size, ShadowAtlas.Size, SceneRenderer.DepthFormat, TextureUsage.DepthStencil | TextureUsage.ShaderRead)
        {
            Name = "Shadow atlas",
        };
        Atlas = device.CreateTexture(atlasDescription);
    }

    public ITexture Atlas { get; }

    public void Draw(ICommandList commands, IReadOnlyList<ShadowView> views, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        if (views.Count > 0)
        {
            commands.Barrier(Atlas, ResourceState.DepthWrite);
            DepthTarget depth = new(Atlas, LoadAction.Clear);
            commands.BeginRendering([], depth);
            commands.SetPipeline(_pipeline);
            foreach (ShadowView view in views)
            {
                DrawView(commands, view, draws, statistics);
            }

            commands.EndRendering();
        }

        commands.Barrier(Atlas, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        Atlas.Dispose();
        _pipeline.Dispose();
    }

    private static void DrawView(ICommandList commands, in ShadowView view, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        ShadowTile tile = view.Tile;
        commands.SetViewport(new Viewport(tile.X, tile.Y, tile.Size, tile.Size));
        commands.SetScissor(tile.X, tile.Y, tile.Size, tile.Size);
        Frustum frustum = new(view.ViewProjection);
        foreach (MeshDraw draw in draws)
        {
            if (draw.Flags.HasFlag(MeshFlags.OutlineOnly))
            {
                continue;
            }

            if (draw.Bounds is { } bounds && !frustum.Intersects(bounds))
            {
                statistics.ShadowCulled++;
                continue;
            }

            statistics.ShadowDrawn++;
            ShadowConstants constants = new(draw.World * view.ViewProjection, draw.Vertices.BindlessIndex);
            commands.SetIndexBuffer(draw.Part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(draw.Part.IndexCount);
        }
    }
}

