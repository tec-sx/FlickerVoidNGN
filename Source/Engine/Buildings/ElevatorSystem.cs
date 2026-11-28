using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// Runs elevators: riding a platform takes it a stop on the way it goes, turning at the top and the bottom, and a call button brings
/// its platform to the button's stop. It hears toggles after the joints do, so its stop wins over the toggle's two ends.
/// </summary>
public sealed class ElevatorSystem
{
    private readonly World _world;
    private readonly CreationParts _parts;

    public ElevatorSystem(World world, EventBus events, CreationParts parts)
    {
        _world = world;
        _parts = parts;
        events.Handle((in ToggledEvent toggled) => Respond(toggled.Target));
    }

    /// <summary>Sends the platform to the stop: at once on its way, without waiting for a ride.</summary>
    public void Send(Entity platform, int stop)
    {
        if (!_world.IsAlive(platform) || !_world.Has<Elevator>(platform) || !_world.Has<JointPose>(platform))
        {
            return;
        }

        ref Elevator elevator = ref _world.Get<Elevator>(platform);
        if (elevator.Stops.Length == 0)
        {
            return;
        }

        int target = Math.Clamp(stop, 0, elevator.Stops.Length - 1);
        if (target != elevator.Stop)
        {
            elevator.Falling = target < elevator.Stop;
        }

        elevator.Stop = target;
        ref JointPose pose = ref _world.Get<JointPose>(platform);
        pose.Target = elevator.Stops[target];
    }

    private void Respond(Entity target)
    {
        if (!_world.IsAlive(target))
        {
            return;
        }

        if (_world.TryGet(target, out ElevatorCall call) && _world.TryGet(target, out CreationPart part))
        {
            Entity platform = _parts.Find(part.Root, call.Platform);
            Send(platform, call.Stop);
            return;
        }

        if (!_world.TryGet(target, out Elevator elevator) || elevator.Stops.Length < 2)
        {
            return;
        }

        bool falling = elevator.Falling;
        if (elevator.Stop >= elevator.Stops.Length - 1)
        {
            falling = true;
        }
        else if (elevator.Stop <= 0)
        {
            falling = false;
        }

        int next = elevator.Stop + 1;
        if (falling)
        {
            next = elevator.Stop - 1;
        }

        Send(target, next);
    }
}
