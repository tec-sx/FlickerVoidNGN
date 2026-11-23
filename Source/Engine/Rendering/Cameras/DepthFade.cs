using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>
/// A painter's fade by depth, measured along the view from a focus such as the protagonist: what stands nearer than the focus
/// darkens and what lies farther melts into a colour, each ramping in between its start and end distances up to its amount.
/// </summary>
public readonly record struct DepthFade(
    float NearStart,
    float NearEnd,
    float NearAmount,
    float FarStart,
    float FarEnd,
    float FarAmount,
    Vector3 FarColor)
{
    /// <summary>Whether it changes the image at all.</summary>
    public bool IsVisible => NearAmount > 0 || FarAmount > 0;

    /// <summary>The fade <paramref name="amount"/> of the way from <paramref name="from"/> to <paramref name="to"/>, every value blended.</summary>
    public static DepthFade Lerp(in DepthFade from, in DepthFade to, float amount)
    {
        return new DepthFade(
            float.Lerp(from.NearStart, to.NearStart, amount),
            float.Lerp(from.NearEnd, to.NearEnd, amount),
            float.Lerp(from.NearAmount, to.NearAmount, amount),
            float.Lerp(from.FarStart, to.FarStart, amount),
            float.Lerp(from.FarEnd, to.FarEnd, amount),
            float.Lerp(from.FarAmount, to.FarAmount, amount),
            Vector3.Lerp(from.FarColor, to.FarColor, amount));
    }
}
