using System.Numerics;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>Moves a value towards another, most of the way (95%) in the given seconds, whatever the frame rate.</summary>
internal static class Smoothing
{
    public static Vector3 Towards(Vector3 current, Vector3 target, float time, float seconds)
    {
        if (time <= 0)
        {
            return target;
        }

        float exponent = -3 * seconds / time;
        float follow = 1 - MathF.Exp(exponent);
        return Vector3.Lerp(current, target, follow);
    }

    /// <summary>The offset shortened to the radius when it reaches further.</summary>
    public static Vector3 Limit(Vector3 offset, float radius)
    {
        float length = offset.Length();
        if (length <= radius || length == 0)
        {
            return offset;
        }

        return offset * (radius / length);
    }
}
