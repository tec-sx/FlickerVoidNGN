using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Vehicles.Tests;

/// <summary>Builds a game with vehicles and physics over a wide floor whose top is at height 0, and places and drives a vehicle on it.</summary>
internal sealed class VehicleDriveTestsBuilder : IDisposable
{
    public const string File = "Vehicles/Van.fvvehicle";

    private readonly VehicleEntityTestsBuilder _vehicles = new();

    public VehicleDriveTestsBuilder(VehicleFile file)
    {
        _vehicles.WithVehicle(File, file).Build();
        Physics = Game.AddPhysics();
        BoxCollider floor = new() { Size = new Vector3(400, 1, 400), Offset = new Vector3(0, -0.5f, 0) };
        Game.World.Create(new Transform(), new StaticBody { Collider = floor });
    }

    public Game Game => _vehicles.Game;

    public World World => Game.World;

    public IPhysicsSystem Physics { get; }

    public CreationParts Parts => _vehicles.Parts;

    /// <summary>Places the vehicle on the floor and lets a step pass, so its parts stand in the simulation.</summary>
    public Entity Place()
    {
        Entity vehicle = _vehicles.Place(File);
        Run(0.1f);
        return vehicle;
    }

    /// <summary>Puts a driver in the vehicle and lets it settle on its wheels for a second.</summary>
    public void Enter(Entity vehicle)
    {
        World.Add(vehicle, new DrivenVehicle());
        Run(1);
    }

    public void Leave(Entity vehicle) => World.Remove<DrivenVehicle>(vehicle);

    public void Drive(Entity vehicle, float forward, float right = 0)
    {
        World.Set(vehicle, new DriveInput { Forward = forward, Right = right });
    }

    public Transform PlaceOf(Entity entity) => World.Get<Transform>(entity);

    public void Run(float seconds)
    {
        int steps = (int)(seconds * 60);
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }
    }

    public void Dispose() => _vehicles.Dispose();
}
