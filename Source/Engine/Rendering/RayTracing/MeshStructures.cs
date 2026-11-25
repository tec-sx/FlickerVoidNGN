using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.RayTracing;

/// <summary>
/// The bottom-level acceleration structures of the mesh parts a view draws: one for each part as it was loaded, shared by every
/// mesh drawing it and built once, and one for each posed part of each animated mesh, refitted every frame to the pose skinning
/// wrote. The structures no draw used in a frame are released.
/// </summary>
internal sealed class MeshStructures(IGraphicsDevice device) : IDisposable
{
    private static readonly int VertexStride = Marshal.SizeOf<MeshVertex>();

    private readonly Dictionary<Key, IAccelerationStructure> _structures = [];
    private readonly HashSet<Key> _used = [];
    private readonly List<Key> _unused = [];

    /// <summary>Starts a frame: no structure is used yet.</summary>
    public void Begin()
    {
        _used.Clear();
    }

    /// <summary>The structure the draw's part is traced in, adding its build, or for a posed part its refit, to the frame's builds once.</summary>
    public IAccelerationStructure Get(in MeshDraw draw, List<BottomLevelBuild> builds)
    {
        GpuMeshPart part = draw.Part;
        bool posed = !ReferenceEquals(draw.Vertices, part.Vertices);
        int? posedMesh = null;
        if (posed)
        {
            posedMesh = draw.Mesh;
        }

        Key key = new(part.Vertices, posedMesh);
        bool firstUse = _used.Add(key);
        BottomLevelGeometry geometry = new(draw.Vertices, part.VertexCount, VertexStride, part.Indices, part.IndexCount);
        if (_structures.TryGetValue(key, out IAccelerationStructure? structure))
        {
            if (posed && firstUse)
            {
                builds.Add(new BottomLevelBuild(structure, geometry, Refit: true));
            }

            return structure;
        }

        BottomLevelDescription description = new(part.VertexCount, part.IndexCount, Refittable: posed);
        structure = device.CreateBottomLevel(description);
        _structures.Add(key, structure);
        builds.Add(new BottomLevelBuild(structure, geometry));
        return structure;
    }

    /// <summary>Releases the structures no draw used since <see cref="Begin"/>, once the GPU is done with them.</summary>
    public void ReleaseUnused()
    {
        _unused.Clear();
        foreach (Key key in _structures.Keys)
        {
            if (!_used.Contains(key))
            {
                _unused.Add(key);
            }
        }

        foreach (Key key in _unused)
        {
            _structures[key].Dispose();
            _structures.Remove(key);
        }
    }

    public void Dispose()
    {
        foreach (IAccelerationStructure structure in _structures.Values)
        {
            structure.Dispose();
        }

        _structures.Clear();
    }

    /// <summary>A part by its own vertex buffer, and the animated mesh posing it, if any.</summary>
    private readonly record struct Key(IBuffer Part, int? PosedMesh);
}
