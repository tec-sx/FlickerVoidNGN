using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Physics.Tests;

/// <summary>
/// Builds a game with physics, a wide floor whose top is at height 0 and a car on it: a box body 1.6 by 0.6 by 4 on four wheels of
/// radius 0.35, the front pair steering and the rear pair driven, facing +Z with its left at +X.
/// </summary>
internal sealed class WheeledBodyTestsBuilder : IDisposable
{
    public const float WheelRadius = 0.35f;

    private WheeledBody _body;

    public WheeledBodyTestsBuilder()
    {
        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        Game = new Game(loggerFactory);
        Physics = Game.AddPhysics();
        BoxCollider floor = new() { Size = new Vector3(400, 1, 400), Offset = new Vector3(0, -0.5f, 0) };
        Game.World.Create(new Transform(), new StaticBody { Collider = floor });
        BoxCollider chassis = new() { Size = new Vector3(1.6f, 0.6f, 4), Offset = new Vector3(0, 0.3f, 0) };
        _body = new WheeledBody
        {
            Collider = chassis,
            Wheels =
            [
                new BodyWheel { Position = new Vector3(0.8f, 0, 1.3f), Radius = WheelRadius, MaxSteer = 35 },
                new BodyWheel { Position = new Vector3(-0.8f, 0, 1.3f), Radius = WheelRadius, MaxSteer = 35 },
                new BodyWheel { Position = new Vector3(0.8f, 0, -1.3f), Radius = WheelRadius, Driven = true, HandBrake = true },
                new BodyWheel { Position = new Vector3(-0.8f, 0, -1.3f), Radius = WheelRadius, Driven = true, HandBrake = true },
            ],
        };
    }

    public Game Game { get; }

    public IPhysicsSystem Physics { get; }

    /// <summary>Places the car with its wheels just above the floor, and lets it settle for a second.</summary>
    public Entity PlaceCar()
    {
        Transform place = new() { Position = new Vector3(0, WheelRadius + 0.05f, 0) };
        Entity car = Game.World.Create(place, _body, new DriveInput());
        Run(1);
        return car;
    }

    public void Drive(Entity car, float forward, float right = 0, float brake = 0)
    {
        Game.World.Set(car, new DriveInput { Forward = forward, Right = right, Brake = brake });
    }

    public Vector3 PositionOf(Entity car) => Game.World.Get<Transform>(car).Position;

    public void Run(float seconds)
    {
        int steps = (int)(seconds * 60);
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }
    }

    public void Dispose() => Game.Dispose();
}
