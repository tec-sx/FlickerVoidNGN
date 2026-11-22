using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Ai.Senses;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Gameplay.Serialization;

namespace FlickerVoid.Gameplay.World.Objects.Doors;

using World = Arch.Core.World;

/// <summary>
/// Swings doors toward open or shut as their toggles change, each fixed step, and rattles them, a noise whoever tried them made, when a
/// lock blocks them and no key opens it; saves keep how far each saveable door is open.
/// </summary>
public sealed class DoorSwingSystem : BaseSystem<World, GameTime>
{
    /// <summary>Seconds a blocked door rattles.</summary>
    public const float RattleTime = 0.35f;

    private const float RattleDegrees = 2.5f;
    private const float RattlePerSecond = 40;

    private static readonly QueryDescription Doors = new QueryDescription().WithAll<DoorSwing, DoorHinge, Transform>();

    private readonly EventBus _events;

    public DoorSwingSystem(Game game)
        : base(game.World)
    {
        _events = game.Events;
        ArrayRegistry.Add<DoorHinge>();
        game.Lifecycle.OnActivate((Entity entity, ref DoorSwing swing) => Activate(entity, swing));
        game.Lifecycle.OnDeactivate((Entity entity, ref DoorSwing _) => Deactivate(entity));
        game.Events.Handle((in ToggledEvent toggled) => Swing(toggled.Target, toggled.Open));
        game.Events.Handle((in ToggleBlockedEvent blocked) => Rattle(blocked.Target, blocked.Instigator));
        game.SaveStates.AddComponent("doorHinge", FlickerVoidJsonContext.Default.DoorHinge);
    }

    public override void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        World.Query(in Doors, (ref DoorSwing swing, ref DoorHinge hinge, ref Transform transform) => Turn(swing, ref hinge, ref transform, seconds));
    }

    private static void Turn(in DoorSwing swing, ref DoorHinge hinge, ref Transform transform, float seconds)
    {
        float step = swing.Speed * seconds;
        float remaining = hinge.Target - hinge.Angle;
        float turn = Math.Clamp(remaining, -step, step);
        hinge.Angle += turn;
        hinge.Rattle = MathF.Max(hinge.Rattle - seconds, 0);

        float shake = 0;
        if (hinge.Rattle > 0)
        {
            float phase = hinge.Rattle * RattlePerSecond;
            shake = MathF.Sin(phase) * RattleDegrees;
        }

        float radians = float.DegreesToRadians(hinge.Angle + shake);
        Quaternion yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians);
        transform.Rotation = Quaternion.Concatenate(yaw, hinge.Closed);
    }

    private void Activate(Entity entity, in DoorSwing swing)
    {
        Quaternion closed = Quaternion.Identity;
        if (World.TryGet(entity, out Transform transform))
        {
            closed = transform.Rotation;
        }

        DoorHinge hinge = new() { Closed = closed };
        if (World.TryGet(entity, out Toggle toggle) && toggle.Open)
        {
            hinge.Angle = swing.OpenAngle;
            hinge.Target = swing.OpenAngle;
        }

        World.Add(entity, hinge);
    }

    private void Deactivate(Entity entity)
    {
        if (World.Has<DoorHinge>(entity))
        {
            World.Remove<DoorHinge>(entity);
        }
    }

    private void Swing(Entity door, bool open)
    {
        if (!World.IsAlive(door) || !World.Has<DoorHinge>(door))
        {
            return;
        }

        float openAngle = World.Get<DoorSwing>(door).OpenAngle;
        ref DoorHinge hinge = ref World.Get<DoorHinge>(door);
        hinge.Target = 0;
        if (open)
        {
            hinge.Target = openAngle;
        }
    }

    private void Rattle(Entity door, Entity instigator)
    {
        if (!World.IsAlive(door) || (World.TryGet(door, out Lockable doorLock) && !doorLock.Locked))
        {
            // A key opened it before the door heard it was blocked.
            return;
        }

        if (World.Has<DoorHinge>(door))
        {
            World.Get<DoorHinge>(door).Rattle = RattleTime;
            World.TryGet(door, out Transform transform);
            _events.Send(new NoiseEvent(instigator, transform.Position));
        }
    }
}
