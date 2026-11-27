using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>
/// Turns the scene's instance batches into draws, one for each part of the model, placed at the identity with their copies on the
/// GPU, culled as a whole against the view (counted in <see cref="RenderStatistics"/>) and drawing their <see cref="InstanceBatch.StandIn"/>
/// beyond its distance from the focus; then culls the copies of the batches in view one by one on the GPU (Instancing.hlsl),
/// which writes the indirect arguments the geometry pass draws them with, and tallies the copies it keeps, read back when the
/// frame's slot comes round again.
/// </summary>
internal sealed class InstancingPass : IRenderPass
{
    private const int GroupSize = 64;

    private readonly IGraphicsDevice _device;
    private readonly MeshLibrary _meshes;
    private readonly MaterialLibrary _materials;
    private readonly IPipeline _cull;
    private readonly IPipeline _finish;
    private readonly IPipeline _clear;
    private readonly IBuffer[] _counted;
    private readonly bool[] _counting;
    private readonly Dictionary<int, BatchBuffers> _buffers = [];
    private readonly HashSet<int> _seen = [];
    private readonly List<int> _gone = [];
    private readonly List<Cull> _culls = [];

    public InstancingPass(IGraphicsDevice device, ShaderLibrary shaders, MeshLibrary meshes, MaterialLibrary materials)
    {
        _device = device;
        _meshes = meshes;
        _materials = materials;
        _cull = CreatePipeline(shaders, "CullMain", "Instance cull");
        _finish = CreatePipeline(shaders, "FinishMain", "Instance cull finish");
        _clear = CreatePipeline(shaders, "ClearMain", "Instance tally clear");
        _counted = new IBuffer[SceneRenderer.MaxFramesInFlight];
        _counting = new bool[SceneRenderer.MaxFramesInFlight];
        for (int slot = 0; slot < _counted.Length; slot++)
        {
            BufferDescription description = new(4, BufferUsage.ShaderWrite, MemoryLocation.Readback) { Name = $"Instance tally {slot}" };
            _counted[slot] = device.CreateBuffer(description);
        }
    }

    public void Prepare(RenderContext context)
    {
        ReadTally(context);
        _culls.Clear();
        _seen.Clear();
        Frustum view = new(context.View.ViewProjection);
        Matrix4x4.Invert(context.View.View, out Matrix4x4 cameraWorld);
        Vector3 focus = context.Focus ?? cameraWorld.Translation;
        foreach (RenderInstanceBatch scene in context.Scene.InstanceBatches)
        {
            InstanceBatch batch = scene.Batch;
            if (batch.Count == 0 || batch.Mesh.Length == 0)
            {
                continue;
            }

            _seen.Add(scene.Id);
            AddDraws(context, scene.Id, batch, view, focus);
        }

        ReleaseGone();
    }

    public void Execute(RenderContext context)
    {
        ICommandList commands = context.Commands;
        IBuffer counted = _counted[context.FrameSlot];
        commands.Barrier(counted, ResourceState.ShaderWrite);
        CullConstants clear = new(context.FrameBuffer, 0, 0, 0, 0, 0, counted.BindlessIndex, 0, Vector3.Zero, 0);
        commands.SetPipeline(_clear);
        commands.PushConstants(clear);
        commands.Dispatch(1);
        _counting[context.FrameSlot] = true;
        foreach (Cull cull in _culls)
        {
            Dispatch(commands, cull, context.FrameBuffer, counted);
        }
    }

    public void Dispose()
    {
        foreach (BatchBuffers buffers in _buffers.Values)
        {
            buffers.Dispose();
        }

        foreach (IBuffer counted in _counted)
        {
            counted.Dispose();
        }

        _clear.Dispose();
        _finish.Dispose();
        _cull.Dispose();
    }

    /// <summary>How far the box is from the point: none from inside it.</summary>
    private static float DistanceTo(in Bounds bounds, Vector3 point)
    {
        Vector3 nearest = Vector3.Clamp(point, bounds.Min, bounds.Max);
        return Vector3.Distance(point, nearest);
    }

    private IPipeline CreatePipeline(ShaderLibrary shaders, string entryPoint, string name)
    {
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load("Instancing", entryPoint), Name = name };
        return _device.CreateComputePipeline(description);
    }

    /// <summary>The copies the GPU kept the last time this frame's slot was drawn, once that frame is done.</summary>
    private void ReadTally(RenderContext context)
    {
        int slot = context.FrameSlot;
        if (!_counting[slot])
        {
            return;
        }

        Span<byte> bytes = stackalloc byte[4];
        _counted[slot].Read(bytes);
        context.Statistics.CopiesDrawn = BitConverter.ToInt32(bytes);
    }

    private void AddDraws(RenderContext context, int id, InstanceBatch batch, in Frustum view, Vector3 focus)
    {
        if (!_buffers.TryGetValue(id, out BatchBuffers? buffers))
        {
            buffers = new BatchBuffers(_device, SceneRenderer.MaxFramesInFlight);
            _buffers.Add(id, buffers);
        }

        GpuModel model = _meshes.Get(batch.Mesh, context.Meshes);
        ModelSphere sphere = ModelSphere.Of(model);
        Bounds bounds = buffers.FindBounds(batch, sphere);
        if (batch.StandIn is { Length: > 0 } standIn && DistanceTo(bounds, focus) > batch.StandInDistance)
        {
            model = _meshes.Get(standIn, context.Meshes);
            sphere = ModelSphere.Of(model);
        }

        RenderStatistics statistics = context.Statistics;
        bool inView = view.Intersects(bounds);
        if (inView)
        {
            statistics.BatchesDrawn++;
            statistics.CopiesSubmitted += batch.Count;
        }
        else
        {
            statistics.BatchesCulled++;
        }

        IBuffer copies = buffers.Update(batch, model, context.FrameSlot);
        IBuffer visible = buffers.Visible!;
        IBuffer arguments = buffers.Arguments!;
        for (int index = 0; index < model.Parts.Count; index++)
        {
            GpuMeshPart part = model.Parts[index];
            string? materialPath = null;
            if (batch.Materials is { } materials && index < materials.Length)
            {
                materialPath = materials[index];
            }

            GpuMaterial material = _materials.Get(materialPath, context.Textures);
            InstancedDraw instanced = new(batch, copies, visible, arguments, index);
            MeshDraw draw = new(id, Matrix4x4.Identity, Matrix4x4.Identity, part, part.Vertices, part.Vertices, material, null, MeshFlags.None, bounds, inView, instanced);
            context.Draws.Add(draw);
        }

        if (batch.ShadowMesh is { Length: > 0 } shadowMesh)
        {
            AddShadowDraws(context, id, shadowMesh, new InstancedDraw(batch, copies, visible, arguments, 0), bounds);
        }

        if (inView)
        {
            uint scales = batch.PackScales();
            _culls.Add(new Cull(copies, visible, arguments, batch.Count, model.Parts.Count, scales, sphere));
        }
    }

    /// <summary>Adds the shadow mesh's parts, drawn with the batch's copies into shadow maps alone.</summary>
    private void AddShadowDraws(RenderContext context, int id, string shadowMesh, InstancedDraw batchDraw, in Bounds bounds)
    {
        GpuModel model = _meshes.Get(shadowMesh, context.Meshes);
        GpuMaterial material = _materials.Get(null, context.Textures);
        for (int index = 0; index < model.Parts.Count; index++)
        {
            GpuMeshPart part = model.Parts[index];
            InstancedDraw instanced = batchDraw with { Part = index };
            MeshDraw draw = new(id, Matrix4x4.Identity, Matrix4x4.Identity, part, part.Vertices, part.Vertices, material, null, MeshFlags.ShadowMap, bounds, false, instanced);
            context.Draws.Add(draw);
        }
    }

    private void Dispatch(ICommandList commands, in Cull cull, uint frame, IBuffer counted)
    {
        CullConstants constants = new(
            frame,
            cull.Copies.BindlessIndex,
            cull.Visible.BindlessIndex,
            cull.Arguments.BindlessIndex,
            (uint)cull.Count,
            (uint)cull.Parts,
            counted.BindlessIndex,
            cull.Scales,
            cull.Sphere.Center,
            cull.Sphere.Radius);
        commands.Barrier(cull.Visible, ResourceState.ShaderWrite);
        commands.Barrier(cull.Arguments, ResourceState.ShaderWrite);
        commands.SetPipeline(_cull);
        commands.PushConstants(constants);
        commands.Dispatch((cull.Count + GroupSize - 1) / GroupSize);
        commands.Barrier(cull.Arguments, ResourceState.ShaderWrite);
        commands.Barrier(counted, ResourceState.ShaderWrite);
        commands.SetPipeline(_finish);
        commands.PushConstants(constants);
        commands.Dispatch(1);
        commands.Barrier(cull.Arguments, ResourceState.IndirectArgument);
        commands.Barrier(cull.Visible, ResourceState.ShaderRead);
    }

    /// <summary>Releases the buffers of batches the scene no longer holds; the device frees them once the GPU is done with them.</summary>
    private void ReleaseGone()
    {
        _gone.Clear();
        foreach (int id in _buffers.Keys)
        {
            if (!_seen.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (int id in _gone)
        {
            _buffers[id].Dispose();
            _buffers.Remove(id);
        }
    }

    /// <summary>A batch in view whose copies the GPU culls this frame.</summary>
    private readonly record struct Cull(IBuffer Copies, IBuffer Visible, IBuffer Arguments, int Count, int Parts, uint Scales, ModelSphere Sphere);
}
