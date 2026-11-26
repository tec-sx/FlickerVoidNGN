using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Creations.Tests;

/// <summary>Builds the joint system under test over the parts builder's world, with a placed creation whose one part has a joint.</summary>
internal sealed class JointSystemTestsBuilder : IDisposable
{
    private readonly CreationPartsTestsBuilder _parts = new();

    public JointSystemTestsBuilder()
    {
        System = new JointSystem(_parts.World, _parts.Spawner.Lifecycle, Events);
    }

    public World World => _parts.World;

    public EventBus Events { get; } = new();

    public CreationParts Parts => _parts.Parts;

    public JointSystem System { get; }

    public Entity Root { get; private set; }

    public Entity Part { get; private set; }

    /// <summary>Places a creation at <paramref name="position"/> whose part <c>door</c> rests at <paramref name="rest"/> on the joint.</summary>
    public JointSystemTestsBuilder Place(Vector3 rest, PartJoint joint, Vector3 position = default)
    {
        Transform place = new() { Position = rest };
        PartData door = new PartData("door", place).With(joint);
        _parts.Redescribe(door);
        Root = _parts.Place(position);
        Part = _parts.Parts.Find(Root, "door");
        return this;
    }

    /// <summary>Places a creation whose part <c>door</c> has a toggle that starts open or closed.</summary>
    public JointSystemTestsBuilder PlaceDoor(PartJoint joint, bool open)
    {
        Transform place = new() { Position = new Vector3(2, 0, 0) };
        Toggle toggle = new() { Open = open };
        PartData door = new PartData("door", place).With(joint).With(toggle);
        _parts.Redescribe(door);
        Root = _parts.Place(Vector3.Zero);
        Part = _parts.Parts.Find(Root, "door");
        return this;
    }

    public void Toggled(bool open) => Toggled(Part, open);

    public void Toggled(Entity target, bool open)
    {
        Events.Send(new ToggledEvent(target, open, Entity.Null));
        Events.Dispatch();
    }

    public static PartJoint Hinge(Vector3 pivot, float min, float max, float speed)
    {
        return new PartJoint { Kind = JointKind.Hinge, Pivot = pivot, Axis = Vector3.UnitY, Min = min, Max = max, Speed = speed };
    }

    public static PartJoint Slider(Vector3 axis, float min, float max, float speed)
    {
        return new PartJoint { Kind = JointKind.Slider, Axis = axis, Min = min, Max = max, Speed = speed };
    }

    public void Aim(float target)
    {
        ref JointPose pose = ref World.Get<JointPose>(Part);
        pose.Target = target;
    }

    public void Tick(float seconds, bool paused = false)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
        GameTime time = new(elapsed, elapsed, 1, paused);
        System.Update(in time);
    }

    public void MoveRoot(Vector3 position)
    {
        _parts.Move(Root, position);
        _parts.Hierarchy.Update(World);
    }

    public float PositionOfJoint() => World.Get<JointPose>(Part).Position;

    public Transform TransformOfPart() => World.Get<Transform>(Part);

    public void Dispose() => _parts.Dispose();
}
