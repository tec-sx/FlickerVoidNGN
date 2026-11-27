using System.Numerics;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>The plain 3D sound of step 1: gain falling with distance and an equal-power pan by the angle to the listener's right.</summary>
public static class Panning
{
    /// <summary>How far to a side a sound can go, so nothing is ever heard in one ear alone.</summary>
    public const float Width = 0.8f;

    /// <summary>The gain of a sound <paramref name="distance"/> away: full within <paramref name="minDistance"/>, gone at <paramref name="maxDistance"/>.</summary>
    public static float GetAttenuation(float distance, float minDistance, float maxDistance)
    {
        if (distance <= minDistance)
        {
            return 1;
        }

        float range = MathF.Max(maxDistance - minDistance, 0.001f);
        float near = Math.Clamp((maxDistance - distance) / range, 0, 1);
        return near * near;
    }

    /// <summary>The gains to the left and right ears of a sound at <paramref name="source"/>, full within <paramref name="minDistance"/> and gone at <paramref name="maxDistance"/>.</summary>
    public static (float Left, float Right) Compute(Vector3 listener, Vector3 right, Vector3 source, float minDistance, float maxDistance)
    {
        Vector3 toSource = source - listener;
        float distance = toSource.Length();
        float gain = GetAttenuation(distance, minDistance, maxDistance);

        float side = 0;
        if (distance > 0.001f)
        {
            Vector3 direction = toSource / distance;
            side = Vector3.Dot(direction, right) * Width;
        }

        float angle = (side + 1) * MathF.PI / 4;
        return (MathF.Cos(angle) * gain, MathF.Sin(angle) * gain);
    }
}
