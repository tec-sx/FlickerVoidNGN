using System.Numerics;

using Arch.Core;
using Arch.System;

using BepuPhysics;
using BepuPhysics.Collidables;

using BepuUtilities;
using BepuUtilities.Memory;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Physics;

/// <summary>
/// Runs the physics simulation each fixed step: creates bodies for new rigid and static bodies, removes those whose entity or component
/// is gone, moves kinematic and static bodies to transforms that moved, steps, and copies dynamic bodies' poses to their transforms.
/// </summary>
public sealed class PhysicsSystem : BaseSystem<World, GameTime>, IPhysicsSystem
{
    private const float SleepThreshold = 0.01f;

    private static readonly QueryDescription NewRigidBodies = new QueryDescription().WithAll<RigidBody, Transform>().WithNone<BodyHandle>();
    private static readonly QueryDescription NewStaticBodies = new QueryDescription().WithAll<StaticBody, Transform>().WithNone<StaticHandle>();
    private static readonly QueryDescription SimulatedBodies = new QueryDescription().WithAll<Transform, BodyHandle, RigidBody>();
    private static readonly QueryDescription PlacedStatics = new QueryDescription().WithAll<Transform, StaticHandle, StaticBody>();

    private readonly BufferPool _pool = new();
    private readonly ThreadDispatcher _dispatcher;
    private readonly Dictionary<Entity, BodyHandle> _bodies = [];
    private readonly Dictionary<Entity, StaticHandle> _statics = [];
    private readonly Dictionary<BodyHandle, Entity> _bodyEntities = [];
    private readonly Dictionary<StaticHandle, Entity> _staticEntities = [];
    private readonly List<Entity> _pending = [];
    private readonly List<(StaticHandle Handle, StaticDescription Description)> _movedStatics = [];

    public PhysicsSystem(World world)
        : base(world)
    {
        ArrayRegistry.Add<BodyHandle>();
        ArrayRegistry.Add<StaticHandle>();
        _dispatcher = new ThreadDispatcher(Environment.ProcessorCount);
        NarrowPhaseCallbacks narrowPhase = new();
        PoseIntegratorCallbacks poseIntegrator = new();
        SolveDescription solve = new(8, 1);
        Simulation = Simulation.Create(_pool, narrowPhase, poseIntegrator, solve);
    }

    public Simulation Simulation { get; }

    public override void Update(in GameTime t)
    {
        RemoveDepartedBodies();
        AddRigidBodies();
        AddStaticBodies();
        FollowKinematicBodies();
        FollowStaticBodies();

        float dt = (float)t.Elapsed.TotalSeconds;
        Simulation.Timestep(dt, _dispatcher);

        CopyPosesToTransforms();
    }

    public bool RayCast(Ray ray, float maxDistance, out RayHit hit)
    {
        ClosestHitHandler handler = new();
        return RayCast(ray, maxDistance, ref handler, out hit);
    }

    public bool RayCast(Ray ray, float maxDistance, Entity ignored, out RayHit hit)
    {
        ClosestHitHandler handler = new();
        if (World.IsAlive(ignored) && World.TryGet(ignored, out StaticHandle staticHandle))
        {
            handler.Ignores = true;
            handler.Ignored = new CollidableReference(staticHandle);
        }
        else if (World.IsAlive(ignored) && World.TryGet(ignored, out BodyHandle bodyHandle))
        {
            BodyReference body = Simulation.Bodies[bodyHandle];
            handler.Ignores = true;
            handler.Ignored = body.CollidableReference;
        }

        return RayCast(ray, maxDistance, ref handler, out hit);
    }

    private bool RayCast(Ray ray, float maxDistance, ref ClosestHitHandler handler, out RayHit hit)
    {
        hit = default;
        Simulation.RayCast(ray.Origin, ray.Direction, maxDistance, _pool, ref handler);
        if (!handler.Found)
        {
            return false;
        }

        Entity entity;
        bool known;
        if (handler.Collidable.Mobility == CollidableMobility.Static)
        {
            known = _staticEntities.TryGetValue(handler.Collidable.StaticHandle, out entity);
        }
        else
        {
            known = _bodyEntities.TryGetValue(handler.Collidable.BodyHandle, out entity);
        }

        if (!known)
        {
            return false;
        }

        Vector3 point = ray.GetPoint(handler.Distance);
        Vector3 normal = Vector3.Normalize(handler.Normal);
        hit = new RayHit(entity, point, normal, handler.Distance);
        return true;
    }

    public override void Dispose()
    {
        Simulation.Dispose();
        _dispatcher.Dispose();
        _pool.Clear();
    }

    private void RemoveDepartedBodies()
    {
        foreach ((Entity entity, BodyHandle handle) in _bodies)
        {
            bool alive = World.IsAlive(entity);
            if (alive && World.Has<RigidBody>(entity))
            {
                continue;
            }

            BodyReference body = Simulation.Bodies[handle];
            TypedIndex shape = body.Collidable.Shape;
            Simulation.Bodies.Remove(handle);
            Simulation.Shapes.Remove(shape);
            if (alive)
            {
                World.Remove<BodyHandle>(entity);
            }

            _bodies.Remove(entity);
            _bodyEntities.Remove(handle);
        }

        foreach ((Entity entity, StaticHandle handle) in _statics)
        {
            bool alive = World.IsAlive(entity);
            if (alive && World.Has<StaticBody>(entity))
            {
                continue;
            }

            StaticReference body = Simulation.Statics[handle];
            TypedIndex shape = body.Shape;
            Simulation.Statics.Remove(handle);
            Simulation.Shapes.Remove(shape);
            if (alive)
            {
                World.Remove<StaticHandle>(entity);
            }

            _statics.Remove(entity);
            _staticEntities.Remove(handle);
        }
    }

    private void AddRigidBodies()
    {
        CollectPending(NewRigidBodies);
        foreach (Entity entity in _pending)
        {
            RigidBody body = World.Get<RigidBody>(entity);
            Transform transform = World.Get<Transform>(entity);
            RigidPose pose = body.Collider.GetPose(transform.Position, transform.Rotation);
            TypedIndex shape = body.Collider.AddShape(Simulation.Shapes);
            CollidableDescription collidable = new(shape);
            BodyActivityDescription activity = new(SleepThreshold);

            BodyDescription description;
            if (body.Kinematic)
            {
                description = BodyDescription.CreateKinematic(pose, collidable, activity);
            }
            else
            {
                BodyInertia inertia = body.Collider.ComputeInertia(body.Mass);
                description = BodyDescription.CreateDynamic(pose, inertia, collidable, activity);
            }

            BodyHandle handle = Simulation.Bodies.Add(description);
            World.Add(entity, in handle);
            _bodies.Add(entity, handle);
            _bodyEntities.Add(handle, entity);
        }
    }

    private void AddStaticBodies()
    {
        CollectPending(NewStaticBodies);
        foreach (Entity entity in _pending)
        {
            StaticBody body = World.Get<StaticBody>(entity);
            Transform transform = World.Get<Transform>(entity);
            TypedIndex shape = body.Collider.AddShape(Simulation.Shapes);
            RigidPose pose = body.Collider.GetPose(transform.Position, transform.Rotation);
            StaticDescription description = new(pose, shape);
            StaticHandle handle = Simulation.Statics.Add(description);
            World.Add(entity, in handle);
            _statics.Add(entity, handle);
            _staticEntities.Add(handle, entity);
        }
    }

    /// <summary>Gathers the query's entities first, because adding components while a query runs would move them between archetypes.</summary>
    private void CollectPending(in QueryDescription query)
    {
        _pending.Clear();
        World.Query(in query, (Entity entity) => _pending.Add(entity));
    }

    /// <summary>Puts each kinematic body where its transform now places it, waking it so the broad phase follows.</summary>
    private void FollowKinematicBodies()
    {
        Bodies bodies = Simulation.Bodies;
        World.Query(in SimulatedBodies, (ref Transform transform, ref BodyHandle handle, ref RigidBody rigidBody) =>
        {
            if (!rigidBody.Kinematic)
            {
                return;
            }

            RigidPose pose = rigidBody.Collider.GetPose(transform.Position, transform.Rotation);
            BodyReference body = bodies[handle];
            if (body.Pose.Position == pose.Position && body.Pose.Orientation == pose.Orientation)
            {
                return;
            }

            body.Pose = pose;
            body.Velocity = default;
            body.Awake = true;
        });
    }

    /// <summary>Moves the static bodies whose transform moved, such as a building placed again; this is rare, so it checks rather than tracks.</summary>
    private void FollowStaticBodies()
    {
        Statics statics = Simulation.Statics;
        _movedStatics.Clear();
        World.Query(in PlacedStatics, (ref Transform transform, ref StaticHandle handle, ref StaticBody staticBody) =>
        {
            RigidPose pose = staticBody.Collider.GetPose(transform.Position, transform.Rotation);
            StaticReference current = statics[handle];
            if (current.Pose.Position == pose.Position && current.Pose.Orientation == pose.Orientation)
            {
                return;
            }

            StaticDescription description = new(pose, current.Shape);
            _movedStatics.Add((handle, description));
        });

        foreach ((StaticHandle handle, StaticDescription description) in _movedStatics)
        {
            statics.ApplyDescription(handle, description);
        }
    }

    private void CopyPosesToTransforms()
    {
        Bodies bodies = Simulation.Bodies;
        World.Query(in SimulatedBodies, (ref Transform transform, ref BodyHandle handle, ref RigidBody rigidBody) =>
        {
            BodyReference body = bodies[handle];
            if (rigidBody.Kinematic || !body.Awake)
            {
                return;
            }

            RigidPose pose = body.Pose;
            transform.Position = rigidBody.Collider.GetEntityPosition(pose);
            transform.Rotation = pose.Orientation;
        });
    }
}
