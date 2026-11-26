namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>
/// A painter's tone by depth, measured along the view from a focus such as the protagonist, whole objects at a time by their
/// object depth: what stands nearer than the focus falls into shadow (its direct light goes) and is burned (its mid-tones sink),
/// and what lies farther loses its colour, each ramping in between its start and end distances up to its amount.
/// </summary>
public readonly record struct DepthFade(
    float NearStart,
    float NearEnd,
    float NearAmount,
    float NearBurn,
    float FarStart,
    float FarEnd,
    float FarAmount)
{
    /// <summary>Whether it changes the image at all.</summary>
    public bool IsVisible => NearAmount > 0 || NearBurn > 0 || FarAmount > 0;
}
