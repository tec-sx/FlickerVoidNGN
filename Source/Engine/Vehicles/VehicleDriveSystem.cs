using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// Drives vehicles, each fixed step: one that gets a <see cref="DrivenVehicle"/> becomes a body on wheels made from its file, and
/// its parts give up their standing collision; its wheel parts follow the wheels on their suspension; and one whose driver left
/// brakes until it stops, then stands as it did before. Does nothing in a game without physics. A game service, so game code can
/// ask whether a vehicle takes a driver.
/// </summary>
public sealed class VehicleDriveSystem : BaseSystem<World, GameTime>
{
    /// <summary>The speed, in metres a second, under which a vehicle without a driver counts as stopped.</summary>
    private const float StoppedSpeed = 0.2f;

    private static readonly QueryDescription Entered = new QueryDescription().WithAll<Vehicle, DrivenVehicle, Transform>().WithNone<WheeledBody>();
    private static readonly QueryDescription Left = new QueryDescription().WithAll<Vehicle, WheeledBody, DriveInput>().WithNone<DrivenVehicle>();
    private static readonly QueryDescription Rolling = new QueryDescription().WithAll<Vehicle, WheeledBody>();

    private readonly CreationParts _parts;
    private readonly VehicleSpawnSystem _spawner;
    private readonly CreationFiles<VehicleFile> _files;
    private readonly IContentManager _content;
    private readonly Func<IPhysicsSystem?> _findPhysics;
    private readonly Dictionary<Entity, string[]> _wheelIds = [];
    private readonly List<Entity> _changing = [];
    private IPhysicsSystem? _physics;

    internal VehicleDriveSystem(World world, CreationParts parts, VehicleSpawnSystem spawner, CreationFiles<VehicleFile> files, IContentManager content, Func<IPhysicsSystem?> findPhysics)
        : base(world)
    {
        _parts = parts;
        _spawner = spawner;
        _files = files;
        _content = content;
        _findPhysics = findPhysics;
        ArrayRegistry.Add<DrivenVehicle>();
        ArrayRegistry.Add<WheeledBody>();
        ArrayRegistry.Add<DriveInput>();
    }

    public override void Update(in GameTime t)
    {
        _physics ??= _findPhysics();
        if (_physics is null)
        {
            return;
        }

        Collect(Entered);
        foreach (Entity vehicle in _changing)
        {
            Start(vehicle);
        }

        Collect(Left);
        foreach (Entity vehicle in _changing)
        {
            Stop(vehicle, _physics);
        }

        IPhysicsSystem physics = _physics;
        World.Query(in Rolling, (Entity vehicle) => PoseWheels(vehicle, physics));
    }

    /// <summary>Whether the vehicle can take a driver: its file can be read, and it is not a trailer.</summary>
    public bool CanDrive(Entity vehicle)
    {
        if (!World.IsAlive(vehicle) || !World.TryGet(vehicle, out Vehicle placed))
        {
            return false;
        }

        return _files.Read(_content, placed.File) is { CanBeDriven: true };
    }

    private void Collect(QueryDescription query)
    {
        _changing.Clear();
        World.Query(in query, (Entity vehicle) => _changing.Add(vehicle));
    }

    /// <summary>Makes the vehicle a body on wheels where it stands; one whose file can't be read, or a trailer, stays standing and loses its driver.</summary>
    private void Start(Entity vehicle)
    {
        string path = World.Get<Vehicle>(vehicle).File;
        if (_files.Read(_content, path) is not { CanBeDriven: true } file)
        {
            World.Remove<DrivenVehicle>(vehicle);
            return;
        }

        string[] ids = [.. file.GetParts().Skip(1).Take(file.Wheels.Count).Select(part => part.Id)];
        _wheelIds[vehicle] = ids;
        _spawner.SetSolid(vehicle, false);
        WheeledBody body = VehicleWheels.CreateBody(file);
        World.Add(vehicle, body);
        if (!World.Has<DriveInput>(vehicle))
        {
            World.Add(vehicle, new DriveInput());
        }
    }

    /// <summary>Holds the brakes of a vehicle without a driver, and once it has stopped stands it there, solid again.</summary>
    private void Stop(Entity vehicle, IPhysicsSystem physics)
    {
        DriveInput brakes = new() { Brake = 1, HandBrake = 1 };
        World.Set(vehicle, brakes);
        Vector3 velocity = physics.GetVelocity(vehicle);
        if (velocity.Length() > StoppedSpeed)
        {
            return;
        }

        World.Remove<WheeledBody, DriveInput>(vehicle);
        _wheelIds.Remove(vehicle);
        _spawner.SetSolid(vehicle, true);
    }

    /// <summary>Puts each wheel part where its wheel is: on its suspension, steered and turned as it rolled.</summary>
    private void PoseWheels(Entity vehicle, IPhysicsSystem physics)
    {
        if (!_wheelIds.TryGetValue(vehicle, out string[]? ids))
        {
            return;
        }

        for (int index = 0; index < ids.Length; index++)
        {
            Entity wheel = _parts.Find(vehicle, ids[index]);
            if (wheel == Entity.Null || !physics.TryGetWheelPose(vehicle, index, out Transform pose))
            {
                continue;
            }

            Transform posed = World.Get<PartPlace>(wheel).Place;
            posed.Position = pose.Position;
            posed.Rotation = Quaternion.Normalize(pose.Rotation * posed.Rotation);
            TransformHierarchy.Attach(World, wheel, vehicle, posed);
        }
    }
}
