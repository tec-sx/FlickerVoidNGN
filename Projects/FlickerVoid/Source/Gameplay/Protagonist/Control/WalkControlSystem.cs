using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Gameplay.Protagonist.Control;

using World = Arch.Core.World;

/// <summary>
/// Walks the protagonist with <see cref="Move"/> (W, A, S and D) each frame, relative to the camera: up on the screen walks away
/// from it. Steering only changes when the keys do, so a click's walk goes on until a key is pressed; talking stands it still.
/// </summary>
public sealed class WalkControlSystem : BaseSystem<World, GameTime>
{
    public static readonly Tag Move = new("Input.Move");

    private static readonly QueryDescription Protagonists = new QueryDescription().WithAll<Protagonist, CharacterMover>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();

    private readonly EventBus _events;
    private readonly InputActions _actions;
    private readonly DialogueSystem _dialogue;
    private Vector3 _steering;

    public WalkControlSystem(World world, EventBus events, InputActions actions, DialogueSystem dialogue)
        : base(world)
    {
        _events = events;
        _actions = actions;
        _dialogue = dialogue;
    }

    public override void Update(in GameTime t)
    {
        Vector2 value = _actions.GetValue(Move);
        if (_dialogue.IsInConversation)
        {
            value = Vector2.Zero;
        }

        Vector3 direction = Vector3.Zero;
        if (value != Vector2.Zero && TryFindCamera(out Quaternion camera))
        {
            direction = ToGround(value, camera);
        }

        if (direction == _steering)
        {
            return;
        }

        _steering = direction;
        Entity protagonist = FindFirst(Protagonists);
        if (protagonist != Entity.Null)
        {
            _events.Send(new SteerCommand(protagonist, direction));
        }
    }

    /// <summary>
    /// The direction on the ground that a move on the screen means for a camera turned this way: right along the camera's right,
    /// up along its forward and up, flattened, so it works from a level camera to one looking straight down. At most 1 long.
    /// </summary>
    public static Vector3 ToGround(Vector2 value, Quaternion camera)
    {
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, camera);
        Vector3 up = Vector3.Transform(Vector3.UnitY, camera);
        Vector3 right = Vector3.Transform(Vector3.UnitX, camera);
        Vector3 away = Flatten(forward + up);
        Vector3 side = Flatten(right);
        Vector3 direction = (side * value.X) + (away * value.Y);
        if (direction.LengthSquared() > 1)
        {
            direction = Vector3.Normalize(direction);
        }

        return direction;
    }

    private static Vector3 Flatten(Vector3 direction)
    {
        direction.Y = 0;
        if (direction.LengthSquared() < 1e-8f)
        {
            return Vector3.Zero;
        }

        return Vector3.Normalize(direction);
    }

    private bool TryFindCamera(out Quaternion rotation)
    {
        Entity camera = FindFirst(Cameras);
        if (camera == Entity.Null)
        {
            rotation = Quaternion.Identity;
            return false;
        }

        rotation = World.Get<Transform>(camera).Rotation;
        return true;
    }

    private Entity FindFirst(in QueryDescription query)
    {
        Entity found = Entity.Null;
        World.Query(in query, (Entity entity) =>
        {
            if (found == Entity.Null)
            {
                found = entity;
            }
        });
        return found;
    }
}
