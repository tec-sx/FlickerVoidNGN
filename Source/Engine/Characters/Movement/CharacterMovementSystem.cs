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
/// Walks characters to their destinations each fixed step: through the corners of the path the <see cref="PathFinder"/> finds (or in a
/// straight line without one) at their speed, turning to face where they walk, standing on whatever physics finds below them, and
/// sending an <see cref="ArrivedEvent"/> at the last corner; characters steered along a direction (<see cref="SteerCommand"/>) the same
/// way, with no end until the steering ends; and turns characters in place to face a point (<see cref="FaceCommand"/>).
/// </summary>
public sealed class CharacterMovementSystem : BaseSystem<World, GameTime>
{
    /// <summary>How far above its feet a character looks for ground, so it climbs small steps and slopes.</summary>
    public const float StepHeight = 0.4f;

    private const float GroundSearch = 2;

    private static readonly QueryDescription Walking = new QueryDescription().WithAll<CharacterMover, Destination, Transform>();
    private static readonly QueryDescription Turning = new QueryDescription().WithAll<CharacterMover, Facing, Transform>();
    private static readonly QueryDescription Steered = new QueryDescription().WithAll<CharacterMover, Heading, Transform>();

    private readonly EventBus _events;
    private readonly IPhysicsSystem? _physics;
    private readonly List<(Entity Entity, Vector3 Point)> _arrived = [];
    private readonly List<Entity> _faced = [];

    public CharacterMovementSystem(World world, EventBus events, IPhysicsSystem? physics)
        : base(world)
    {
        _events = events;
        _physics = physics;
        ArrayRegistry.Add<Destination>();
        ArrayRegistry.Add<Facing>();
        ArrayRegistry.Add<Heading>();
        events.Handle((in MoveCommand command) => Move(command));
        events.Handle((in SteerCommand command) => Steer(command));
        events.Handle((in FaceCommand command) => Face(command));
        events.Handle((in StopCommand command) => Stop(command.Character));
    }

    /// <summary>Finds the way around obstacles; set by the module that provides paths, such as Ai.</summary>
    public IPathFinder? PathFinder { get; set; }

    public override void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        _arrived.Clear();
        World.Query(in Walking, (Entity entity, ref CharacterMover mover, ref Destination destination, ref Transform transform) =>
        {
            Vector3 corner = destination.Corners[destination.Corner];
            if (!Walk(mover, corner, ref transform, seconds))
            {
                return;
            }

            if (destination.Corner == destination.Corners.Count - 1)
            {
                _arrived.Add((entity, corner));
            }
            else
            {
                destination.Corner++;
            }
        });

        World.Query(in Steered, (ref CharacterMover mover, ref Heading heading, ref Transform transform) =>
            Step(mover, heading.Direction, ref transform, seconds));

        foreach ((Entity entity, Vector3 point) in _arrived)
        {
            World.Remove<Destination>(entity);
            _events.Send(new ArrivedEvent(entity, point));
        }

        _faced.Clear();
        World.Query(in Turning, (Entity entity, ref CharacterMover mover, ref Facing facing, ref Transform transform) =>
        {
            Quaternion turned = Turn(transform.Rotation, facing.Direction, mover.TurnSpeed * seconds);
            transform.Rotation = turned;
            Quaternion target = Face(facing.Direction);
            if (turned == target)
            {
                _faced.Add(entity);
            }
        });

        foreach (Entity entity in _faced)
        {
            World.Remove<Facing>(entity);
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
        Advance(mover, direction, step, ref transform, seconds);
        return distance - step <= mover.ArriveDistance;
    }

    /// <summary>Moves one step along a steered direction, at the speed scaled by its length.</summary>
    private void Step(in CharacterMover mover, Vector3 direction, ref Transform transform, float seconds)
    {
        float length = direction.Length();
        float scale = MathF.Min(length, 1);
        Vector3 unit = direction / length;
        float step = mover.Speed * scale * seconds;
        Advance(mover, unit, step, ref transform, seconds);
    }

    /// <summary>Moves the step along the unit direction on the ground, turning toward it.</summary>
    private void Advance(in CharacterMover mover, Vector3 direction, float step, ref Transform transform, float seconds)
    {
        Vector3 position = transform.Position;
        Vector3 moved = position + (direction * step);
        moved.Y = FindGround(moved, position.Y);
        transform.Position = moved;
        transform.Rotation = Turn(transform.Rotation, direction, mover.TurnSpeed * seconds);
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
        Quaternion target = Face(direction);
        float dot = MathF.Abs(Quaternion.Dot(rotation, target));
        float angle = float.RadiansToDegrees(2 * MathF.Acos(MathF.Min(dot, 1)));
        if (angle <= maxDegrees)
        {
            return target;
        }

        return Quaternion.Slerp(rotation, target, maxDegrees / angle);
    }

    /// <summary>The rotation whose +Z points down the direction on the ground plane.</summary>
    private static Quaternion Face(Vector3 direction)
    {
        float yaw = MathF.Atan2(direction.X, direction.Z);
        return Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
    }

    private void Face(in FaceCommand command)
    {
        Entity character = command.Character;
        if (!World.IsAlive(character) || !World.Has<CharacterMover>(character) || !World.Has<Transform>(character))
        {
            return;
        }

        Stop(character);
        Vector3 direction = command.Point - World.Get<Transform>(character).Position;
        direction.Y = 0;
        if (direction.LengthSquared() < 1e-6f)
        {
            return;
        }

        Facing facing = new() { Direction = Vector3.Normalize(direction) };
        if (World.Has<Facing>(character))
        {
            World.Set(character, facing);
        }
        else
        {
            World.Add(character, facing);
        }
    }

    private void Move(in MoveCommand command)
    {
        Entity character = command.Character;
        if (!World.IsAlive(character) || !World.Has<CharacterMover>(character) || !World.Has<Transform>(character))
        {
            return;
        }

        List<Vector3> corners = [];
        if (PathFinder is null)
        {
            corners.Add(command.Point);
        }
        else
        {
            Vector3 from = World.Get<Transform>(character).Position;
            if (!PathFinder.TryFindPath(from, command.Point, corners) || corners.Count == 0)
            {
                Stop(character);
                return;
            }
        }

        if (World.Has<Facing>(character))
        {
            World.Remove<Facing>(character);
        }

        RemoveHeading(character);
        Destination destination = new() { Point = command.Point, Corners = corners };
        if (World.Has<Destination>(character))
        {
            World.Set(character, destination);
        }
        else
        {
            World.Add(character, destination);
        }
    }

    /// <summary>Steers the character along the direction flattened onto the ground, replacing where it was going; a zero direction ends the steering.</summary>
    private void Steer(in SteerCommand command)
    {
        Entity character = command.Character;
        if (!World.IsAlive(character) || !World.Has<CharacterMover>(character))
        {
            return;
        }

        Vector3 direction = command.Direction;
        direction.Y = 0;
        if (direction.LengthSquared() < 1e-6f)
        {
            RemoveHeading(character);
            return;
        }

        if (World.Has<Destination>(character))
        {
            World.Remove<Destination>(character);
        }

        if (World.Has<Facing>(character))
        {
            World.Remove<Facing>(character);
        }

        Heading heading = new() { Direction = direction };
        if (World.Has<Heading>(character))
        {
            World.Set(character, heading);
        }
        else
        {
            World.Add(character, heading);
        }
    }

    private void Stop(Entity character)
    {
        if (World.IsAlive(character) && World.Has<Destination>(character))
        {
            World.Remove<Destination>(character);
        }

        RemoveHeading(character);
    }

    private void RemoveHeading(Entity character)
    {
        if (World.IsAlive(character) && World.Has<Heading>(character))
        {
            World.Remove<Heading>(character);
        }
    }
}
