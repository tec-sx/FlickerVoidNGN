using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// Moves the jointed parts of creations without physics: each joint's position goes toward its target at its speed within its
/// limits, and the part is placed under its root at its rest place turned about the hinge or slid along the slider, so it still
/// follows the root. A joint starts at its rest place, or its nearest limit, as its part attaches, or open when its part has an open toggle; a toggle that opens or closes sends it to its open position or back to rest. Nothing moves while paused.
/// </summary>
public sealed class JointSystem : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Joints = new QueryDescription().WithAll<CreationPart, PartPlace, PartJoint, JointPose>();

    public JointSystem(World world, EntityLifecycle lifecycle, EventBus events)
        : base(world)
    {
        ArrayRegistry.Add<PartJoint>();
        ArrayRegistry.Add<JointPose>();
        lifecycle.OnAttach((Entity part, ref PartJoint joint) => Start(part, joint));
        events.Handle((in ToggledEvent toggled) => Follow(toggled.Target, toggled.Open));
    }

    public override void Update(in GameTime time)
    {
        if (time.Paused)
        {
            return;
        }

        float seconds = (float)time.Elapsed.TotalSeconds;
        World.Query(in Joints, (Entity entity, ref CreationPart part, ref PartPlace place, ref PartJoint joint, ref JointPose pose) =>
        {
            float target = Math.Clamp(pose.Target, joint.Min, joint.Max);
            float moved = Step(pose.Position, target, joint.Speed, seconds);
            if (moved == pose.Position)
            {
                return;
            }

            pose.Position = moved;
            Transform posed = Pose(place.Place, joint, moved);
            TransformHierarchy.Attach(World, entity, part.Root, posed);
        });
    }

    /// <summary>The rest place moved by the joint's position: turned about the hinge's pivot or slid along the slider's axis.</summary>
    public static Transform Pose(in Transform rest, in PartJoint joint, float position)
    {
        Vector3 axis = Vector3.Normalize(joint.Axis);
        Transform posed = rest;
        if (joint.Kind == JointKind.Slider)
        {
            posed.Position = rest.Position + (axis * position);
            return posed;
        }

        float radians = float.DegreesToRadians(position);
        Quaternion turn = Quaternion.CreateFromAxisAngle(axis, radians);
        Vector3 offset = Vector3.Transform(rest.Position - joint.Pivot, turn);
        posed.Position = joint.Pivot + offset;
        posed.Rotation = Quaternion.Normalize(turn * rest.Rotation);
        return posed;
    }

    private void Start(Entity entity, PartJoint joint)
    {
        float position = Math.Clamp(0, joint.Min, joint.Max);
        if (World.TryGet(entity, out Toggle toggle) && toggle.Open)
        {
            position = joint.GetOpenPosition();
        }

        JointPose pose = new() { Position = position, Target = position };
        if (World.Has<JointPose>(entity))
        {
            World.Set(entity, pose);
        }
        else
        {
            World.Add(entity, pose);
        }

        Place(entity, joint, position);
    }

    private void Place(Entity entity, in PartJoint joint, float position)
    {
        if (World.TryGet(entity, out CreationPart part) && World.TryGet(entity, out PartPlace place))
        {
            Transform posed = Pose(place.Place, joint, position);
            TransformHierarchy.Attach(World, entity, part.Root, posed);
        }
    }

    /// <summary>Puts a part's joint at its open position or at rest at once, as a load does; does nothing for a part without a joint.</summary>
    public void Snap(Entity entity, bool open)
    {
        if (!World.TryGet(entity, out PartJoint joint) || !World.Has<JointPose>(entity))
        {
            return;
        }

        float position = Math.Clamp(0, joint.Min, joint.Max);
        if (open)
        {
            position = joint.GetOpenPosition();
        }

        JointPose pose = new() { Position = position, Target = position };
        World.Set(entity, pose);
        Place(entity, joint, position);
    }

    /// <summary>Sends a door's joint toward open or shut as its toggle changes.</summary>
    private void Follow(Entity entity, bool open)
    {
        if (!World.IsAlive(entity) || !World.TryGet(entity, out PartJoint joint) || !World.Has<JointPose>(entity))
        {
            return;
        }

        float target = Math.Clamp(0, joint.Min, joint.Max);
        if (open)
        {
            target = joint.GetOpenPosition();
        }

        ref JointPose pose = ref World.Get<JointPose>(entity);
        pose.Target = target;
    }

    private static float Step(float position, float target, float speed, float seconds)
    {
        if (speed <= 0)
        {
            return target;
        }

        float distance = speed * seconds;
        if (MathF.Abs(target - position) <= distance)
        {
            return target;
        }

        return position + (MathF.Sign(target - position) * distance);
    }
}
