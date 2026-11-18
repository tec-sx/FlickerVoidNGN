using Arch.Core;
using Arch.System;

using BepuPhysics;
using BepuPhysics.Collidables;

using BepuUtilities;
using BepuUtilities.Memory;

using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Physics;

/// <summary>Runs the physics simulation each fixed step: creates bodies for new rigid and static bodies, removes those whose entity or component is gone, steps, and copies body poses to transforms.</summary>
public sealed class PhysicsSystem : BaseSystem<World, GameTime>, IPhysicsSystem
{
    private const float SleepThreshold = 0.01f;

    private static readonly QueryDescription NewRigidBodies = new QueryDescription().WithAll<RigidBody, Transform>().WithNone<BodyHandle>();
    private static readonly QueryDescription NewStaticBodies = new QueryDescription().WithAll<StaticBody, Transform>().WithNone<StaticHandle>();
    private static readonly QueryDescription SimulatedBodies = new QueryDescription().WithAll<Transform, BodyHandle>();

    private readonly BufferPool _pool = new();
    private readonly ThreadDispatcher _dispatcher;
    private readonly Dictionary<Entity, BodyHandle> _bodies = [];
    private readonly Dictionary<Entity, StaticHandle> _statics = [];
    private readonly List<Entity> _pending = [];

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

        float dt = (float)t.Elapsed.TotalSeconds;
        Simulation.Timestep(dt, _dispatcher);

        CopyPosesToTransforms();
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
        }
    }

    private void AddRigidBodies()
    {
        CollectPending(NewRigidBodies);
        foreach (Entity entity in _pending)
        {
            RigidBody body = World.Get<RigidBody>(entity);
            Transform transform = World.Get<Transform>(entity);
            RigidPose pose = new(transform.Position, transform.Rotation);
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
            StaticDescription description = new(transform.Position, transform.Rotation, shape);
            StaticHandle handle = Simulation.Statics.Add(description);
            World.Add(entity, in handle);
            _statics.Add(entity, handle);
        }
    }

    /// <summary>Gathers the query's entities first, because adding components while a query runs would move them between archetypes.</summary>
    private void CollectPending(in QueryDescription query)
    {
        _pending.Clear();
        World.Query(in query, (Entity entity) => _pending.Add(entity));
    }

    private void CopyPosesToTransforms()
    {
        Bodies bodies = Simulation.Bodies;
        World.Query(in SimulatedBodies, (ref Transform transform, ref BodyHandle handle) =>
        {
            BodyReference body = bodies[handle];
            if (!body.Awake)
            {
                return;
            }

            transform.Position = body.Pose.Position;
            transform.Rotation = body.Pose.Orientation;
        });
    }
}
