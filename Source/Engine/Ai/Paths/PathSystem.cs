using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>
/// Finds paths for characters around the bodies that are obstacles (a fixed system, a service and the movement system's
/// <see cref="IPathFinder"/>): the navigation mesh is built when first asked for a path, and an obstacle that starts, stops, or comes to
/// rest somewhere new builds again only the tiles it touched and touches. One that is still moving changes nothing until it stops.
/// </summary>
public sealed class PathSystem : BaseSystem<World, GameTime>, IPathFinder
{
    private readonly NavigationSettings _settings;
    private readonly List<float> _vertices = [];
    private readonly List<int> _triangles = [];
    private readonly Dictionary<Entity, TrackedObstacle> _obstacles = [];
    private readonly List<Bounds> _changed = [];
    private readonly List<Entity> _gone = [];
    private readonly Action<Entity, Bounds> _found;
    private bool _built;

    public PathSystem(World world, EntityLifecycle lifecycle, NavigationSettings settings)
        : base(world)
    {
        _settings = settings;
        _found = Found;
        lifecycle.OnActivate((Entity entity, ref StaticBody _) => Track(entity));
        lifecycle.OnDeactivate((Entity entity, ref StaticBody _) => Untrack(entity));
        lifecycle.OnActivate((Entity entity, ref RigidBody _) => Track(entity));
        lifecycle.OnDeactivate((Entity entity, ref RigidBody _) => Untrack(entity));
    }

    /// <summary>The walkable ground as last built; null before the first path or when there is none.</summary>
    public NavigationMesh? Mesh { get; private set; }

    /// <summary>Builds the whole mesh again before the next path.</summary>
    public void Invalidate() => _built = false;

    /// <summary>Notes the obstacles that came to rest somewhere new, or are gone without being stopped.</summary>
    public override void Update(in GameTime t)
    {
        foreach ((Entity entity, TrackedObstacle obstacle) in _obstacles)
        {
            if (!World.IsAlive(entity) || !World.TryGet(entity, out Transform transform))
            {
                _gone.Add(entity);
                continue;
            }

            if (transform.Position != obstacle.Position || transform.Rotation != obstacle.Rotation)
            {
                obstacle.Position = transform.Position;
                obstacle.Rotation = transform.Rotation;
                obstacle.Moving = true;
                continue;
            }

            if (!obstacle.Moving)
            {
                continue;
            }

            obstacle.Moving = false;
            if (!ObstacleGeometry.TryGetObstacle(World, entity, out Collider collider))
            {
                continue;
            }

            Bounds bounds = ObstacleGeometry.GetBounds(collider, transform);
            if (bounds != obstacle.Bounds)
            {
                _changed.Add(obstacle.Bounds);
                _changed.Add(bounds);
                obstacle.Bounds = bounds;
            }
        }

        foreach (Entity entity in _gone)
        {
            Untrack(entity);
        }

        _gone.Clear();
    }

    public bool TryFindPath(Vector3 from, Vector3 to, List<Vector3> corners)
    {
        Build();
        if (Mesh is null)
        {
            return false;
        }

        return Mesh.TryFindPath(from, to, corners);
    }

    public Vector3 Constrain(Vector3 from, Vector3 to)
    {
        Build();
        if (Mesh is null)
        {
            return to;
        }

        return Mesh.Constrain(from, to);
    }

    /// <summary>A random point of the ground within the radius of a point, such as where a resident wanders next; false when there is none.</summary>
    public bool TryFindRandomPoint(Vector3 around, float radius, Random random, out Vector3 point)
    {
        Build();
        if (Mesh is null)
        {
            point = around;
            return false;
        }

        return Mesh.TryFindRandomPoint(around, radius, random, out point);
    }

    /// <summary>Builds the whole mesh the first time, and after that the tiles of what changed, if anything did.</summary>
    private void Build()
    {
        if (_built && _changed.Count == 0)
        {
            return;
        }

        _vertices.Clear();
        _triangles.Clear();
        if (!_built)
        {
            _obstacles.Clear();
            ObstacleGeometry.Collect(World, _vertices, _triangles, _found);
            Mesh = NavigationMesh.Build(_vertices, _triangles, _settings);
            _built = true;
            _changed.Clear();
            return;
        }

        ObstacleGeometry.Collect(World, _vertices, _triangles);
        bool rebuilt = Mesh is not null && Mesh.Rebuild(_vertices, _triangles, _changed);
        if (!rebuilt)
        {
            Mesh = NavigationMesh.Build(_vertices, _triangles, _settings);
        }

        _changed.Clear();
    }

    private void Found(Entity entity, Bounds bounds)
    {
        Transform transform = World.Get<Transform>(entity);
        _obstacles[entity] = new TrackedObstacle(transform.Position, transform.Rotation, bounds);
    }

    private void Track(Entity entity)
    {
        if (!ObstacleGeometry.TryGetObstacle(World, entity, out Collider collider) || !World.TryGet(entity, out Transform transform))
        {
            return;
        }

        Bounds bounds = ObstacleGeometry.GetBounds(collider, transform);
        if (_obstacles.TryGetValue(entity, out TrackedObstacle? known))
        {
            if (known.Bounds == bounds)
            {
                return;
            }

            _changed.Add(known.Bounds);
        }

        _obstacles[entity] = new TrackedObstacle(transform.Position, transform.Rotation, bounds);
        _changed.Add(bounds);
    }

    private void Untrack(Entity entity)
    {
        if (_obstacles.Remove(entity, out TrackedObstacle? known))
        {
            _changed.Add(known.Bounds);
        }
    }

    /// <summary>Where an obstacle was last seen and what it covered when paths were last built around it.</summary>
    private sealed class TrackedObstacle(Vector3 position, Quaternion rotation, Bounds bounds)
    {
        public Vector3 Position { get; set; } = position;

        public Quaternion Rotation { get; set; } = rotation;

        public Bounds Bounds { get; set; } = bounds;

        public bool Moving { get; set; }
    }
}
