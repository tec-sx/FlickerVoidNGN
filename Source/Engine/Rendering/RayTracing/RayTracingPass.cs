using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Instancing;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.RayTracing;

/// <summary>
/// Builds the scene's acceleration structures while the frame traces rays (<see cref="RenderContext.RayTracing"/>): the mesh
/// parts' bottom-level structures (<see cref="MeshStructures"/>), then the top-level one placing every draw but outline-only
/// ones, in the view or not, each with its draw's number as its id and cutout draws not opaque, so shaders test their paint. It
/// runs after skinning, so posed meshes are traced in this frame's pose, and before the geometry pass; without ray tracing it
/// holds nothing. A batch's part places each of its copies, the batches nearest the focus first, until
/// <see cref="MaxTracedCopies"/> are placed.
/// </summary>
internal sealed class RayTracingPass : IRenderPass
{
    private const int MinimumInstances = 64;

    /// <summary>The most batch copies the scene places, so a field of grass can't fill it.</summary>
    public const int MaxTracedCopies = 1 << 16;

    private readonly IGraphicsDevice _device;
    private readonly MeshStructures _meshes;
    private readonly IBuffer?[] _instances = new IBuffer?[SceneRenderer.MaxFramesInFlight];
    private readonly List<BottomLevelBuild> _builds = [];
    private readonly List<(float Distance, int Draw)> _batchDraws = [];
    private readonly Dictionary<int, int> _tracedCopies = [];
    private AccelerationStructureInstance[] _placed = [];
    private IAccelerationStructure? _scene;

    public RayTracingPass(IGraphicsDevice device)
    {
        _device = device;
        _meshes = new MeshStructures(device);
    }

    public void Prepare(RenderContext context)
    {
        if (!context.RayTracing)
        {
            Release();
            return;
        }

        int traced = PlanCopies(context);
        context.AccelerationStructure = EnsureScene(context.Draws.Count + traced);
    }

    public void Execute(RenderContext context)
    {
        if (context.AccelerationStructure is not { } scene)
        {
            return;
        }

        _builds.Clear();
        _meshes.Begin();
        int count = 0;
        for (int index = 0; index < context.Draws.Count; index++)
        {
            MeshDraw draw = context.Draws[index];
            // Rays pass through glass, and see a batch's model rather than its shadow mesh.
            if ((draw.Flags & (MeshFlags.OutlineOnly | MeshFlags.ShadowMap)) != 0 || draw.Material.Transparent || draw.Part.IndexCount == 0)
            {
                continue;
            }

            IAccelerationStructure mesh = _meshes.Get(draw, _builds);
            bool opaque = !draw.Material.Cutout;
            if (draw.Instanced is { } instanced)
            {
                count = PlaceCopies(instanced, index, mesh, opaque, count);
                continue;
            }

            _placed[count] = new AccelerationStructureInstance(draw.World, (uint)index, mesh, opaque: opaque);
            count++;
        }

        _meshes.ReleaseUnused();
        ICommandList commands = context.Commands;
        commands.BuildBottomLevels(CollectionsMarshal.AsSpan(_builds));
        IBuffer instances = EnsureInstances(context.FrameSlot);
        ReadOnlySpan<AccelerationStructureInstance> placed = _placed.AsSpan(0, count);
        instances.Write(MemoryMarshal.AsBytes(placed));
        commands.BuildTopLevel(scene, instances, count);
    }

    public void Dispose()
    {
        Release();
    }

    /// <summary>How many copies each batch's part places, by its draw's number, the batches nearest the focus first; gives the sum.</summary>
    private int PlanCopies(RenderContext context)
    {
        _batchDraws.Clear();
        _tracedCopies.Clear();
        Matrix4x4.Invert(context.View.View, out Matrix4x4 cameraWorld);
        Vector3 focus = context.Focus ?? cameraWorld.Translation;
        for (int index = 0; index < context.Draws.Count; index++)
        {
            MeshDraw draw = context.Draws[index];
            if (draw.Instanced is not null && !draw.Flags.HasFlag(MeshFlags.ShadowMap) && draw.Bounds is { } bounds)
            {
                float distance = Vector3.Distance(bounds.Center, focus);
                _batchDraws.Add((distance, index));
            }
        }

        _batchDraws.Sort();
        int left = MaxTracedCopies;
        int total = 0;
        foreach ((_, int index) in _batchDraws)
        {
            int copies = Math.Min(context.Draws[index].Instanced!.Batch.Count, left);
            _tracedCopies[index] = copies;
            left -= copies;
            total += copies;
        }

        return total;
    }

    /// <summary>Places the planned copies of a batch's part; gives how many instances are placed in all.</summary>
    private int PlaceCopies(InstancedDraw instanced, int index, IAccelerationStructure mesh, bool opaque, int count)
    {
        InstanceBatch batch = instanced.Batch;
        int copies = _tracedCopies.GetValueOrDefault(index);
        ReadOnlySpan<Instance> placed = batch.Copies[..copies];
        foreach (Instance copy in placed)
        {
            Matrix4x4 world = copy.ToMatrix(batch.MinScale, batch.MaxScale);
            _placed[count] = new AccelerationStructureInstance(world, (uint)index, mesh, opaque: opaque);
            count++;
        }

        return count;
    }

    /// <summary>A top-level structure for at least that many draws; a larger one replaces a smaller one, released once the GPU is done with it.</summary>
    private IAccelerationStructure EnsureScene(int drawCount)
    {
        if (_scene is not null && _placed.Length >= drawCount)
        {
            return _scene;
        }

        _scene?.Dispose();
        int capacity = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(drawCount, MinimumInstances));
        TopLevelDescription description = new(capacity) { Name = "Scene" };
        _scene = _device.CreateTopLevel(description);
        _placed = new AccelerationStructureInstance[capacity];
        return _scene;
    }

    /// <summary>The frame slot's buffer of instances, big enough for the top-level structure's.</summary>
    private IBuffer EnsureInstances(int slot)
    {
        long size = (long)_placed.Length * Marshal.SizeOf<AccelerationStructureInstance>();
        IBuffer? instances = _instances[slot];
        if (instances is not null && instances.Description.Size >= size)
        {
            return instances;
        }

        instances?.Dispose();
        BufferDescription description = new(size, BufferUsage.AccelerationStructureInput, MemoryLocation.Upload) { Name = $"Scene instances {slot}" };
        instances = _device.CreateBuffer(description);
        _instances[slot] = instances;
        return instances;
    }

    private void Release()
    {
        if (_scene is null)
        {
            return;
        }

        _meshes.Dispose();
        _scene.Dispose();
        _scene = null;
        _placed = [];
        for (int slot = 0; slot < _instances.Length; slot++)
        {
            _instances[slot]?.Dispose();
            _instances[slot] = null;
        }
    }
}
