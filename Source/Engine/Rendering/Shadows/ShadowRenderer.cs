using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// Draws the depth of every mesh into the shadow atlas, once per shadow view it is in, leaving the atlas ready to sample; parts in a
/// cutout material leave out their holes, and a batch's part in a view draws all its copies at once. A tile keeps what it drew while
/// its view and the casters in it stay the same: still casters are drawn into a second atlas only when they change, and copied into
/// the sampled one under the posed casters, which move with their pose and are drawn anew each frame they are in the tile.
/// </summary>
internal sealed class ShadowRenderer : IDisposable
{
    private const string ShaderName = "Shadow";

    private readonly IPipeline _solid;
    private readonly IPipeline _cutout;
    private readonly ITexture _still;
    private readonly Dictionary<ShadowTile, int> _kept = [];
    private HashSet<ShadowTile> _posed = [];
    private HashSet<ShadowTile> _wasPosed = [];
    private readonly List<TileWork> _work = [];

    public ShadowRenderer(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _solid = CreatePipeline(device, shaders, "PixelMain", "Shadow");
        _cutout = CreatePipeline(device, shaders, "CutoutMain", "Shadow cutout");
        TextureDescription atlasDescription = new(ShadowAtlas.Size, ShadowAtlas.Size, SceneRenderer.DepthFormat, TextureUsage.DepthStencil | TextureUsage.ShaderRead | TextureUsage.CopyDestination)
        {
            Name = "Shadow atlas",
        };
        Atlas = device.CreateTexture(atlasDescription);
        TextureDescription stillDescription = atlasDescription with { Usage = TextureUsage.DepthStencil | TextureUsage.CopySource, Name = "Still shadow atlas" };
        _still = device.CreateTexture(stillDescription);
    }

    public ITexture Atlas { get; }

    /// <summary>
    /// The box a posed part stays in, as it has none of its own: its rest pose's box grown by the box's own size on every side, as a
    /// pose bends a part about its joints.
    /// </summary>
    public static Bounds PosedBounds(in MeshDraw draw)
    {
        Bounds rest = draw.Part.Bounds.Transform(draw.World);
        Vector3 size = rest.Max - rest.Min;
        float reach = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        Vector3 grow = new(reach);
        return new Bounds(rest.Min - grow, rest.Max + grow);
    }

    /// <summary>Draws the views' depth; <paramref name="frameBuffer"/> holds the draws' records, which cutout parts read.</summary>
    public void Draw(ICommandList commands, uint frameBuffer, IReadOnlyList<ShadowView> views, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        PlanTiles(views, draws, statistics);
        DrawStill(commands, frameBuffer, draws, statistics);
        CopyStill(commands);
        DrawPosed(commands, frameBuffer, draws, statistics);
        commands.Barrier(Atlas, ResourceState.ShaderRead);
        (_wasPosed, _posed) = (_posed, _wasPosed);
    }

    public void Dispose()
    {
        _still.Dispose();
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

    private static bool Casts(in MeshDraw draw)
    {
        // Light passes through glass, and a batch with a shadow mesh casts through that alone.
        if (draw.Flags.HasFlag(MeshFlags.OutlineOnly) || draw.Material.Transparent)
        {
            return false;
        }

        bool replaced = draw.Instanced is { Batch.ShadowMesh.Length: > 0 } && !draw.Flags.HasFlag(MeshFlags.ShadowMap);
        return !replaced;
    }

    /// <summary>Which tiles draw their still casters again, which are copied and which have posed casters, by what is in each view.</summary>
    private void PlanTiles(IReadOnlyList<ShadowView> views, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        _work.Clear();
        _posed.Clear();
        foreach (ShadowView view in views)
        {
            Frustum frustum = new(view.ViewProjection);
            HashCode still = new();
            still.Add(view.ViewProjection);
            bool posed = false;
            for (int index = 0; index < draws.Count; index++)
            {
                MeshDraw draw = draws[index];
                if (!Casts(draw))
                {
                    continue;
                }

                if (draw.Bounds is not { } bounds)
                {
                    Bounds reach = PosedBounds(draw);
                    posed |= frustum.Intersects(reach);
                    continue;
                }

                if (frustum.Intersects(bounds))
                {
                    AddCaster(ref still, draw);
                }
            }

            int key = still.ToHashCode();
            bool stale = !_kept.TryGetValue(view.Tile, out int kept) || kept != key;
            _kept[view.Tile] = key;
            if (posed)
            {
                _posed.Add(view.Tile);
            }

            bool copy = stale || posed || _wasPosed.Contains(view.Tile);
            _work.Add(new TileWork(view, frustum, stale, copy, posed));
            if (stale)
            {
                statistics.ShadowTilesDrawn++;
            }
            else
            {
                statistics.ShadowTilesKept++;
            }
        }
    }

    private static void AddCaster(ref HashCode still, in MeshDraw draw)
    {
        still.Add(draw.Mesh);
        still.Add(draw.Part);
        still.Add(draw.World);
        still.Add(draw.Material);
        if (draw.Instanced is { } instanced)
        {
            still.Add(instanced.Batch);
            still.Add(instanced.Batch.Version);
            still.Add(instanced.Batch.Count);
            still.Add(instanced.Batch.MinScale);
            still.Add(instanced.Batch.MaxScale);
        }
    }

    private void DrawStill(ICommandList commands, uint frameBuffer, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        bool begun = false;
        foreach (TileWork work in _work)
        {
            if (!work.Stale)
            {
                continue;
            }

            if (!begun)
            {
                commands.Barrier(_still, ResourceState.DepthWrite);
                commands.BeginRendering([], new DepthTarget(_still, LoadAction.Load));
                begun = true;
            }

            ShadowTile tile = work.View.Tile;
            SetTile(commands, tile);
            commands.ClearDepth(tile.X, tile.Y, tile.Size, tile.Size);
            DrawCasters(commands, frameBuffer, work, draws, statistics, posed: false);
        }

        if (begun)
        {
            commands.EndRendering();
        }
    }

    private void CopyStill(ICommandList commands)
    {
        bool begun = false;
        foreach (TileWork work in _work)
        {
            if (!work.Copy)
            {
                continue;
            }

            if (!begun)
            {
                commands.Barrier(_still, ResourceState.CopySource);
                commands.Barrier(Atlas, ResourceState.CopyDestination);
                begun = true;
            }

            ShadowTile tile = work.View.Tile;
            commands.CopyTextureRegion(_still, Atlas, tile.X, tile.Y, tile.Size, tile.Size);
        }
    }

    private void DrawPosed(ICommandList commands, uint frameBuffer, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics)
    {
        bool begun = false;
        foreach (TileWork work in _work)
        {
            if (!work.Posed)
            {
                continue;
            }

            if (!begun)
            {
                commands.Barrier(Atlas, ResourceState.DepthWrite);
                commands.BeginRendering([], new DepthTarget(Atlas, LoadAction.Load));
                begun = true;
            }

            SetTile(commands, work.View.Tile);
            DrawCasters(commands, frameBuffer, work, draws, statistics, posed: true);
        }

        if (begun)
        {
            commands.EndRendering();
        }
    }

    private static void SetTile(ICommandList commands, ShadowTile tile)
    {
        commands.SetViewport(new Viewport(tile.X, tile.Y, tile.Size, tile.Size));
        commands.SetScissor(tile.X, tile.Y, tile.Size, tile.Size);
    }

    /// <summary>Draws the view's still casters or its posed ones.</summary>
    private void DrawCasters(ICommandList commands, uint frameBuffer, in TileWork work, IReadOnlyList<MeshDraw> draws, RenderStatistics statistics, bool posed)
    {
        ShadowView view = work.View;
        IPipeline? current = null;
        for (int index = 0; index < draws.Count; index++)
        {
            MeshDraw draw = draws[index];
            if (!Casts(draw) || (draw.Bounds is null) != posed)
            {
                continue;
            }

            Bounds bounds = draw.Bounds ?? PosedBounds(draw);
            if (!work.Frustum.Intersects(bounds))
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

            uint copies = SceneConstants.None;
            int instances = 1;
            if (draw.Instanced is { } instanced)
            {
                copies = instanced.Copies.BindlessIndex;
                instances = instanced.Batch.Count;
            }

            ShadowConstants constants = new(draw.World * view.ViewProjection, draw.Vertices.BindlessIndex, frameBuffer, (uint)index, copies);
            commands.SetIndexBuffer(draw.Part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(draw.Part.IndexCount, instances);
        }
    }

    /// <summary>What a shadow view does this frame: draw its still casters again, copy them into the sampled atlas, draw posed casters over them.</summary>
    private readonly record struct TileWork(ShadowView View, Frustum Frustum, bool Stale, bool Copy, bool Posed);
}
