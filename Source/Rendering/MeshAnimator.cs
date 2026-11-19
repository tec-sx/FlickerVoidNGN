namespace FlickerVoid.Engine.Rendering;

/// <summary>Plays one of a skinned mesh renderer's animations, looping; a <see cref="MeshAnimationSystem"/> advances its time.</summary>
public struct MeshAnimator
{
    public MeshAnimator()
    {
    }

    /// <summary>The animation's name in the model; null plays the first one.</summary>
    public string? Animation { get; set; }

    /// <summary>How far into the animation the mesh is, in seconds.</summary>
    public float Time { get; set; }

    /// <summary>How many seconds of animation play per second of game time.</summary>
    public float Speed { get; set; } = 1;
}
