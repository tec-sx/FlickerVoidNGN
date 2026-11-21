using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>
/// Walks characters to their destinations each fixed step: along the ground in a straight line at their speed, turning to face where
/// they walk, standing on whatever physics finds below them, and sending an <see cref="ArrivedEvent"/> when they get there.
/// </summary>
public sealed class CharacterMovementSystem : BaseSystem<World, GameTime>
{
    /// <summary>How far above its feet a character looks for ground, so it climbs small steps and slopes.</summary>
    public const float StepHeight = 0.4f;

    private const float GroundSearch = 2;

    private static readonly QueryDescription Walking = new QueryDescription().WithAll<CharacterMover, Destination, Transform>();

    private readonly EventBus _events;
    private readonly IPhysicsSystem? _physics;
    private readonly List<(Entity Entity, Vector3 Point)> _arrived = [];

    public CharacterMovementSystem(World world, EventBus events, IPhysicsSystem? physics)
        : base(world)
    {
        _events = events;
        _physics = physics;
        ArrayRegistry.Add<Destination>();
        events.Handle((in MoveCommand command) => Move(command));
        events.Handle((in StopCommand command) => Stop(command.Character));
    }

    public override void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        _arrived.Clear();
        World.Query(in Walking, (Entity entity, ref CharacterMover mover, ref Destination destination, ref Transform transform) =>
        {
            if (Walk(mover, destination.Point, ref transform, seconds))
            {
                _arrived.Add((entity, destination.Point));
            }
        });

        foreach ((Entity entity, Vector3 point) in _arrived)
        {
            World.Remove<Destination>(entity);
            _events.Send(new ArrivedEvent(entity, point));
        }
    }

    /// <summary>Moves one step toward the point; true once there.</summary>
    private bool Walk(in CharacterMover mover, Vector3 point, ref Transform transform, float seconds)
    {
        Vector3 position = transform.Position;
        Vector3 toPoint = point - position;
        toPoint.Y = 0;
        float distance = toPoint.Length();
        if (distance <= mover.ArriveDistance)
        {
            return true;
        }

        Vector3 direction = toPoint / distance;
        float step = MathF.Min(mover.Speed * seconds, distance);
        Vector3 moved = position + (direction * step);
        moved.Y = FindGround(moved, position.Y);
        transform.Position = moved;
        transform.Rotation = Turn(transform.Rotation, direction, mover.TurnSpeed * seconds);
        return distance - step <= mover.ArriveDistance;
    }

    /// <summary>The height of the ground under a point, or the old height where physics finds none.</summary>
    private float FindGround(Vector3 point, float height)
    {
        if (_physics is null)
        {
            return height;
        }

        Vector3 origin = new(point.X, height + StepHeight, point.Z);
        Ray down = new(origin, -Vector3.UnitY);
        if (_physics.RayCast(down, GroundSearch, out RayHit hit))
        {
            return hit.Point.Y;
        }

        return height;
    }

    /// <summary>Turns the yaw toward the direction by at most the given degrees; a character faces down its +Z axis, the front of a glTF model.</summary>
    private static Quaternion Turn(Quaternion rotation, Vector3 direction, float maxDegrees)
    {
        float yaw = MathF.Atan2(direction.X, direction.Z);
        Quaternion target = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
        float dot = MathF.Abs(Quaternion.Dot(rotation, target));
        float angle = float.RadiansToDegrees(2 * MathF.Acos(MathF.Min(dot, 1)));
        if (angle <= maxDegrees)
        {
            return target;
        }

        return Quaternion.Slerp(rotation, target, maxDegrees / angle);
    }

    private void Move(in MoveCommand command)
    {
        Entity character = command.Character;
        if (!World.IsAlive(character) || !World.Has<CharacterMover>(character))
        {
            return;
        }

        Destination destination = new() { Point = command.Point };
        if (World.Has<Destination>(character))
        {
            World.Set(character, destination);
        }
        else
        {
            World.Add(character, destination);
        }
    }

    private void Stop(Entity character)
    {
        if (World.IsAlive(character) && World.Has<Destination>(character))
        {
            World.Remove<Destination>(character);
        }
    }
}
