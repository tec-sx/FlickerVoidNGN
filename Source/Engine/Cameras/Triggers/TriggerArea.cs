using System.Numerics;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>
/// A box in its entity's space that <see cref="TriggerActivator"/>s set off by walking into it and out of it; physics has no
/// trigger volumes, so <see cref="TriggerAreaSystem"/> checks it. Cameras cue from it, and music or story can later.
/// </summary>
public struct TriggerArea
{
    public TriggerArea()
    {
    }

    /// <summary>The box's size along the entity's axes, in metres.</summary>
    public Vector3 Size { get; set; } = Vector3.One;

    /// <summary>Where the box's centre is, in the entity's space.</summary>
    public Vector3 Offset { get; set; }

    /// <summary>Tags an activator must own, every one, to set the area off.</summary>
    public List<Tag> RequiredTags { get; set; } = [];
}
