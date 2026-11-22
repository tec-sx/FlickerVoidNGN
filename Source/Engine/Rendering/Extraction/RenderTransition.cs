using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// What a camera director lays over the frame while it switches shots: the previous shot's live view dissolving away over the
/// new one, then a fade to a colour, each from 0 (none) to 1 (all).
/// </summary>
public readonly record struct RenderTransition(float Fade, Vector3 FadeColor, float Dissolve, RenderCamera DissolveFrom)
{
    public bool IsVisible => Fade > 0 || Dissolve > 0;
}
