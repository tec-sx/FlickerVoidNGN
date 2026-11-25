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
}
