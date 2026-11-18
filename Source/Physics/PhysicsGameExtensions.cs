using FlickerVoid.Engine.Runtime;

namespace FlickerVoid.Engine.Physics;

/// <summary>Adds physics to a game.</summary>
public static class PhysicsGameExtensions
{
    /// <summary>Runs a <see cref="PhysicsSystem"/> in the game's fixed steps, registers it as a service, and lets scene files add "rigidBody" and "staticBody" components.</summary>
    public static PhysicsSystem AddPhysics(this Game game)
    {
        PhysicsSystem physics = new(game.World);
        game.FixedSystems.Add(physics);
        game.Services.AddService(physics);
        game.Spawner.Register("rigidBody", PhysicsJsonContext.Default.RigidBody);
        game.Spawner.Register("staticBody", PhysicsJsonContext.Default.StaticBody);
        return physics;
    }
}
