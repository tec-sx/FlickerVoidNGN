using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// A skin animation baked at <see cref="FrameRate"/> frames per second: for each frame, the matrix of every joint that takes
/// a vertex from the bind pose to its place in the model. Sampling blends the two nearest frames and loops past the end.
/// </summary>
public sealed class AnimationClip
{
    public const float FrameRate = 30;

    private readonly Matrix4x4[] _frames;

    /// <summary>Takes the frames one after another, each holding <paramref name="jointCount"/> matrices.</summary>
    public AnimationClip(string name, float duration, int jointCount, Matrix4x4[] frames)
    {
        if (jointCount <= 0 || frames.Length == 0 || frames.Length % jointCount != 0)
        {
            throw new ArgumentException($"{frames.Length} matrices don't make whole frames of {jointCount} joints.", nameof(frames));
        }

        Name = name;
        Duration = duration;
        JointCount = jointCount;
        _frames = frames;
    }

    public string Name { get; }

    /// <summary>The length of one loop, in seconds.</summary>
    public float Duration { get; }

    public int JointCount { get; }

    public int FrameCount => _frames.Length / JointCount;

    /// <summary>Writes the joints' matrices at <paramref name="time"/> seconds, looping, into <paramref name="skin"/>.</summary>
    public void Sample(float time, Span<Matrix4x4> skin)
    {
        float looped = 0;
        if (Duration > 0)
        {
            looped = time % Duration;
            if (looped < 0)
            {
                looped += Duration;
            }
        }

        float position = looped * FrameRate;
        int first = Math.Min((int)position, FrameCount - 1);
        int second = Math.Min(first + 1, FrameCount - 1);
        float amount = position - first;
        ReadOnlySpan<Matrix4x4> from = _frames.AsSpan(first * JointCount, JointCount);
        ReadOnlySpan<Matrix4x4> to = _frames.AsSpan(second * JointCount, JointCount);
        for (int joint = 0; joint < JointCount; joint++)
        {
            skin[joint] = Matrix4x4.Lerp(from[joint], to[joint], amount);
        }
    }
}
