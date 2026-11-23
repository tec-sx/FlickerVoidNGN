using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Physics;

/// <summary>Adds physics to a game.</summary>
public static class PhysicsGameExtensions
{
    /// <summary>Runs a <see cref="PhysicsSystem"/> in the game's fixed steps, registers it as a service, lets scene files add "rigidBody" and "staticBody" components, and gives solid greyboxes static boxes while playing.</summary>
    public static IPhysicsSystem AddPhysics(this Game game)
    {
        IPhysicsSystem physics = new PhysicsSystem(game.World);
        game.FixedSystems.Add(physics);
        game.Services.AddService(physics);
        game.Spawner.Register("rigidBody", PhysicsJsonContext.Default.RigidBody);
        game.Spawner.Register("staticBody", PhysicsJsonContext.Default.StaticBody);
        World world = game.World;
        game.Lifecycle.OnActivate((Entity entity, ref Greybox greybox) => AddGreyboxBody(world, entity, greybox));
        return physics;
    }

    /// <summary>Gives a solid greybox without a body of its own a static box of its size, standing on the entity as the box does.</summary>
    private static void AddGreyboxBody(World world, Entity entity, Greybox greybox)
    {
        if (!greybox.Solid || world.Has<StaticBody>(entity) || world.Has<RigidBody>(entity))
        {
            return;
        }

        Vector3 offset = new(0, greybox.Size.Y / 2, 0);
        BoxCollider collider = new() { Size = greybox.Size, Offset = offset };
        StaticBody body = new() { Collider = collider };
        world.Add(entity, body);
    }
}
