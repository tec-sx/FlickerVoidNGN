using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;

using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics;

/// <summary>
/// Runs the physics simulation on Jolt each fixed step: creates bodies for new rigid and static bodies, removes those whose entity or component
/// is gone, moves kinematic and static bodies to transforms that moved, steps, and copies awake dynamic bodies' poses to their transforms.
/// </summary>
public sealed class PhysicsSystem : BaseSystem<World, GameTime>, IPhysicsSystem
{
    private const uint MaxBodies = 65536;
    private const uint MaxBodyPairs = 65536;
    private const uint MaxContactConstraints = 10240;
    private const float Friction = 1;
    private const float Damping = 0.05f;

    /// <summary>Adding more bodies than this in one step, as when a level loads, rebuilds the broad phase so queries stay fast.</summary>
    private const int OptimizeAfter = 64;

    private static readonly Vector3 Gravity = new(0, -9.8f, 0);
    private static readonly Jolt.ObjectLayer StaticLayer = new(0);
    private static readonly Jolt.ObjectLayer MovingLayer = new(1);

    private static readonly QueryDescription NewRigidBodies = new QueryDescription().WithAll<RigidBody, Transform>().WithNone<SimulatedBody>();
    private static readonly QueryDescription NewStaticBodies = new QueryDescription().WithAll<StaticBody, Transform>().WithNone<SimulatedBody>();
    private static readonly QueryDescription SimulatedBodies = new QueryDescription().WithAll<Transform, SimulatedBody, RigidBody>();
    private static readonly QueryDescription PlacedStatics = new QueryDescription().WithAll<Transform, SimulatedBody, StaticBody>();

    private readonly Jolt.ObjectLayerPairFilterTable _layerPairs;
    /// <summary>Jolt's C API steps every simulation with the one temporary allocator it starts with, so only one may step at a time.</summary>
    private static readonly Lock StepLock = new();

    private readonly Jolt.BroadPhaseLayerInterfaceTable _broadPhaseLayers;
    private readonly Jolt.ObjectVsBroadPhaseLayerFilterTable _layerFilter;
    private readonly Jolt.PhysicsSystem _system;
    private readonly Jolt.JobSystemThreadPool _jobs;
    private readonly Jolt.BodyInterface _bodies;
    private readonly Jolt.BodyLockInterface _locks;
    private readonly Jolt.NarrowPhaseQuery _query;
    private readonly IgnoredBodyFilter _ignored = new();
    private readonly Dictionary<Entity, (Jolt.BodyID Id, bool Static)> _ids = [];
    private readonly Dictionary<Jolt.BodyID, Entity> _entities = [];
    private readonly List<Entity> _pending = [];
    private readonly List<(Jolt.BodyID Id, Vector3 Position, Quaternion Rotation)> _movedStatics = [];
    private int _added;

    static PhysicsSystem()
    {
        if (!Jolt.Foundation.Init())
        {
            throw new InvalidOperationException("Jolt could not start.");
        }
    }

    public PhysicsSystem(World world)
        : base(world)
    {
        ArrayRegistry.Add<SimulatedBody>();
        _layerPairs = new Jolt.ObjectLayerPairFilterTable(2);
        _layerPairs.EnableCollision(StaticLayer, MovingLayer);
        _layerPairs.EnableCollision(MovingLayer, MovingLayer);
        _broadPhaseLayers = new Jolt.BroadPhaseLayerInterfaceTable(2, 2);
        _broadPhaseLayers.MapObjectToBroadPhaseLayer(StaticLayer, new Jolt.BroadPhaseLayer(0));
        _broadPhaseLayers.MapObjectToBroadPhaseLayer(MovingLayer, new Jolt.BroadPhaseLayer(1));
        _layerFilter = new Jolt.ObjectVsBroadPhaseLayerFilterTable(_broadPhaseLayers, 2, _layerPairs, 2);
        Jolt.PhysicsSystemSettings settings = new()
        {
            MaxBodies = (int)MaxBodies,
            MaxBodyPairs = (int)MaxBodyPairs,
            MaxContactConstraints = (int)MaxContactConstraints,
            ObjectLayerPairFilter = _layerPairs,
            BroadPhaseLayerInterface = _broadPhaseLayers,
            ObjectVsBroadPhaseLayerFilter = _layerFilter,
        };
        _system = new Jolt.PhysicsSystem(settings);
        _system.Gravity = Gravity;

        // Jolt reads 0 threads as one per core, so the calling thread is left out explicitly.
        int workers = Math.Max(1, Environment.ProcessorCount - 1);
        Jolt.JobSystemThreadPoolConfig jobs = new() { maxJobs = 1024, maxBarriers = 8, numThreads = workers };
        _jobs = new Jolt.JobSystemThreadPool(in jobs);
        _bodies = _system.BodyInterface;
        _locks = _system.BodyLockInterface;
        _query = _system.NarrowPhaseQuery;
    }

    public int BodyCount => (int)_system.BodiesCount;

    public bool HasBody(Entity entity)
    {
        return _ids.TryGetValue(entity, out (Jolt.BodyID Id, bool Static) body) && _bodies.IsAdded(body.Id);
    }

    public bool IsAwake(Entity entity)
    {
        return _ids.TryGetValue(entity, out (Jolt.BodyID Id, bool Static) body) && _bodies.IsActive(body.Id);
    }

    public override void Update(in GameTime t)
    {
        float dt = (float)t.Elapsed.TotalSeconds;
        _added = 0;
        RemoveDepartedBodies();
        AddRigidBodies();
        AddStaticBodies();
        FollowKinematicBodies(dt);
        FollowStaticBodies();
        if (_added > OptimizeAfter)
        {
            _system.OptimizeBroadPhase();
        }

        lock (StepLock)
        {
            _system.Update(dt, 1, _jobs);
        }

        CopyPosesToTransforms();
    }

    public bool RayCast(Ray ray, float maxDistance, out RayHit hit)
    {
        return CastRay(ray, maxDistance, null, out hit);
    }

    public bool RayCast(Ray ray, float maxDistance, Entity ignored, out RayHit hit)
    {
        if (!_ids.TryGetValue(ignored, out (Jolt.BodyID Id, bool Static) body))
        {
            return CastRay(ray, maxDistance, null, out hit);
        }

        _ignored.Ignored = body.Id;
        return CastRay(ray, maxDistance, _ignored, out hit);
    }

    public override void Dispose()
    {
        _system.Dispose();
        _jobs.Dispose();
        _ignored.Dispose();
        _layerFilter.Dispose();
        _broadPhaseLayers.Dispose();
        _layerPairs.Dispose();
    }

    private bool CastRay(Ray ray, float maxDistance, Jolt.BodyFilter? filter, out RayHit hit)
    {
        hit = default;
        Jolt.Ray cast = new(ray.Origin, ray.Direction * maxDistance);
        bool found = _query.CastRay(in cast, out Jolt.RayCastResult result, null, null, filter);
        if (!found || !_entities.TryGetValue(result.BodyID, out Entity entity))
        {
            return false;
        }

        float distance = result.Fraction * maxDistance;
        Vector3 point = ray.GetPoint(distance);
        Vector3 normal = GetSurfaceNormal(result, point);
        hit = new RayHit(entity, point, normal, distance);
        return true;
    }

    private Vector3 GetSurfaceNormal(in Jolt.RayCastResult result, Vector3 point)
    {
        Vector3 normal = Vector3.Zero;
        _locks.LockRead(result.BodyID, out Jolt.BodyLockRead bodyLock);
        Jolt.Body? body = bodyLock.Body;
        if (body is not null)
        {
            Jolt.SubShapeID subShape = new(result.subShapeID2);
            normal = body.GetWorldSpaceSurfaceNormal(in subShape, in point);
        }

        _locks.UnlockRead(in bodyLock);
        return normal;
    }

    private void RemoveDepartedBodies()
    {
        foreach ((Entity entity, (Jolt.BodyID Id, bool Static) body) in _ids)
        {
            bool alive = World.IsAlive(entity);
            if (alive && HasBodyComponent(entity, body.Static))
            {
                continue;
            }

            _bodies.RemoveAndDestroyBody(body.Id);
            if (alive)
            {
                World.Remove<SimulatedBody>(entity);
            }

            _ids.Remove(entity);
            _entities.Remove(body.Id);
        }
    }

    private bool HasBodyComponent(Entity entity, bool isStatic)
    {
        if (isStatic)
        {
            return World.Has<StaticBody>(entity);
        }

        return World.Has<RigidBody>(entity);
    }

    private void AddRigidBodies()
    {
        CollectPending(NewRigidBodies);
        foreach (Entity entity in _pending)
        {
            RigidBody body = World.Get<RigidBody>(entity);
            Transform transform = World.Get<Transform>(entity);
            Jolt.MotionType motion = body.Kinematic ? Jolt.MotionType.Kinematic : Jolt.MotionType.Dynamic;
            using Jolt.BodyCreationSettings settings = CreateSettings(body.Collider, transform, motion, MovingLayer);
            if (!body.Kinematic)
            {
                settings.OverrideMassProperties = Jolt.OverrideMassProperties.CalculateInertia;
                settings.MassPropertiesOverride = new Jolt.MassProperties { Mass = body.Mass };
            }

            Add(entity, transform, settings, false);
        }
    }

    private void AddStaticBodies()
    {
        CollectPending(NewStaticBodies);
        foreach (Entity entity in _pending)
        {
            StaticBody body = World.Get<StaticBody>(entity);
            Transform transform = World.Get<Transform>(entity);
            using Jolt.BodyCreationSettings settings = CreateSettings(body.Collider, transform, Jolt.MotionType.Static, StaticLayer);
            Add(entity, transform, settings, true);
        }
    }

    private static Jolt.BodyCreationSettings CreateSettings(Collider collider, in Transform transform, Jolt.MotionType motion, Jolt.ObjectLayer layer)
    {
        Vector3 position = collider.GetBodyPosition(transform.Position, transform.Rotation);
        using Jolt.Shape shape = collider.CreateShape();
        Jolt.BodyCreationSettings settings = new(shape, position, transform.Rotation, motion, layer)
        {
            Friction = Friction,
            Restitution = 0,
            LinearDamping = Damping,
            AngularDamping = Damping,
        };
        return settings;
    }

    private void Add(Entity entity, in Transform transform, Jolt.BodyCreationSettings settings, bool isStatic)
    {
        Jolt.Activation activation = isStatic ? Jolt.Activation.DontActivate : Jolt.Activation.Activate;
        Jolt.BodyID id = _bodies.CreateAndAddBody(settings, activation);
        if (id.IsInvalid)
        {
            throw new InvalidOperationException($"The physics simulation is full: it holds at most {MaxBodies} bodies.");
        }

        SimulatedBody simulated = new() { Id = id, PlacedPosition = transform.Position, PlacedRotation = transform.Rotation };
        World.Add(entity, in simulated);
        _ids.Add(entity, (id, isStatic));
        _entities.Add(id, entity);
        _added++;
    }

    /// <summary>Gathers the query's entities first, because adding components while a query runs would move them between archetypes.</summary>
    private void CollectPending(in QueryDescription query)
    {
        _pending.Clear();
        World.Query(in query, (Entity entity) => _pending.Add(entity));
    }

    /// <summary>Moves each kinematic body to where its transform now places it over the step, so it pushes what it meets, and stops it once its transform stands still.</summary>
    private void FollowKinematicBodies(float dt)
    {
        Jolt.BodyInterface bodies = _bodies;
        World.Query(in SimulatedBodies, (ref Transform transform, ref SimulatedBody simulated, ref RigidBody rigidBody) =>
        {
            if (!rigidBody.Kinematic)
            {
                return;
            }

            bool moved = transform.Position != simulated.PlacedPosition || transform.Rotation != simulated.PlacedRotation;
            if (!moved && !simulated.Moving)
            {
                return;
            }

            Vector3 position = rigidBody.Collider.GetBodyPosition(transform.Position, transform.Rotation);
            bodies.MoveKinematic(simulated.Id, position, transform.Rotation, dt);
            simulated.PlacedPosition = transform.Position;
            simulated.PlacedRotation = transform.Rotation;
            simulated.Moving = moved;
        });
    }

    /// <summary>
    /// Moves the static bodies whose transform moved, such as a building placed again, and wakes what touched them before or after; this is
    /// rare, so it checks rather than tracks.
    /// </summary>
    private void FollowStaticBodies()
    {
        _movedStatics.Clear();
        World.Query(in PlacedStatics, (ref Transform transform, ref SimulatedBody simulated, ref StaticBody staticBody) =>
        {
            if (transform.Position == simulated.PlacedPosition && transform.Rotation == simulated.PlacedRotation)
            {
                return;
            }

            Vector3 position = staticBody.Collider.GetBodyPosition(transform.Position, transform.Rotation);
            _movedStatics.Add((simulated.Id, position, transform.Rotation));
            simulated.PlacedPosition = transform.Position;
            simulated.PlacedRotation = transform.Rotation;
        });

        foreach ((Jolt.BodyID id, Vector3 position, Quaternion rotation) in _movedStatics)
        {
            WakeBodiesAround(id);
            _bodies.SetPositionAndRotation(id, position, rotation, Jolt.Activation.DontActivate);
            WakeBodiesAround(id);
        }
    }

    /// <summary>Wakes the moving bodies within a static body's bounds, so a crate falls when the shelf under it moves away.</summary>
    private void WakeBodiesAround(Jolt.BodyID id)
    {
        _locks.LockRead(id, out Jolt.BodyLockRead bodyLock);
        Jolt.Body? body = bodyLock.Body;
        Jolt.BoundingBox bounds = default;
        if (body is not null)
        {
            bounds = body.WorldSpaceBounds;
        }

        _locks.UnlockRead(in bodyLock);
        if (body is not null)
        {
            _system.ActivateBodiesInAABox(in bounds, StaticLayer);
        }
    }

    private void CopyPosesToTransforms()
    {
        Jolt.BodyInterface bodies = _bodies;
        World.Query(in SimulatedBodies, (ref Transform transform, ref SimulatedBody simulated, ref RigidBody rigidBody) =>
        {
            if (rigidBody.Kinematic || !bodies.IsActive(simulated.Id))
            {
                return;
            }

            Vector3 position = bodies.GetPosition(simulated.Id);
            Quaternion rotation = bodies.GetRotation(simulated.Id);
            transform.Position = rigidBody.Collider.GetEntityPosition(position, rotation);
            transform.Rotation = rotation;
        });
    }
}
