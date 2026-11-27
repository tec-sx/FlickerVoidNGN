using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// Sends an elevator's platform to its next stop each time it is used: up a storey at a time, and from the top back to the bottom.
/// It hears the platform's toggle after the joints do, so its stop wins over the toggle's two ends.
/// </summary>
public sealed class ElevatorSystem
{
    private readonly World _world;

    public ElevatorSystem(World world, EventBus events)
    {
        _world = world;
        events.Handle((in ToggledEvent toggled) => Ride(toggled.Target));
    }

    private void Ride(Entity platform)
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

        elevator.Stop = (elevator.Stop + 1) % elevator.Stops.Length;
        ref JointPose pose = ref _world.Get<JointPose>(platform);
        pose.Target = elevator.Stops[elevator.Stop];
    }
}
