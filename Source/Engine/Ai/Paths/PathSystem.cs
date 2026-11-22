using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>
/// Finds paths for characters over the static bodies of the world (a service and the movement system's <see cref="IPathFinder"/>): the
/// navigation mesh is built when first asked for a path and again after static bodies start or stop.
/// </summary>
public sealed class PathSystem : IPathFinder
{
    private readonly World _world;
    private readonly NavigationSettings _settings;
    private readonly List<float> _vertices = [];
    private readonly List<int> _triangles = [];
    private bool _dirty = true;

    public PathSystem(World world, EntityLifecycle lifecycle, NavigationSettings settings)
    {
        _world = world;
        _settings = settings;
        lifecycle.OnActivate((Entity _, ref StaticBody _) => _dirty = true);
        lifecycle.OnDeactivate((Entity _, ref StaticBody _) => _dirty = true);
    }

    /// <summary>The walkable ground as last built; null before the first path or when there is none.</summary>
    public NavigationMesh? Mesh { get; private set; }

    /// <summary>Builds the mesh again before the next path, such as after a static body moved.</summary>
    public void Invalidate() => _dirty = true;

    public bool TryFindPath(Vector3 from, Vector3 to, List<Vector3> corners)
    {
        if (_dirty)
        {
            Build();
        }

        if (Mesh is null)
        {
            return false;
        }

        return Mesh.TryFindPath(from, to, corners);
    }

    /// <summary>A random point of the ground within the radius of a point, such as where a resident wanders next; false when there is none.</summary>
    public bool TryFindRandomPoint(Vector3 around, float radius, Random random, out Vector3 point)
    {
        if (_dirty)
        {
            Build();
        }

        if (Mesh is null)
        {
            point = around;
            return false;
        }

        return Mesh.TryFindRandomPoint(around, radius, random, out point);
    }

    private void Build()
    {
        _dirty = false;
        _vertices.Clear();
        _triangles.Clear();
        StaticGeometry.Collect(_world, _vertices, _triangles);
        float[] vertices = [.. _vertices];
        int[] triangles = [.. _triangles];
        Mesh = NavigationMesh.Build(vertices, triangles, _settings);
    }
}
