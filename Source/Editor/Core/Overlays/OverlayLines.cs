namespace FlickerVoid.Editor.Core.Overlays;

/// <summary>How wide the editor's lines over the viewport are, in pixels, so every tool draws the same thin lines.</summary>
public static class OverlayLines
{
    /// <summary>Outlines, guides and whatever is only shown.</summary>
    public const float Thin = 1;

    /// <summary>What is selected, hovered or being dragged.</summary>
    public const float Bold = 1.5f;

    /// <summary>A gizmo's handles, wide enough to take hold of.</summary>
    public const float Handle = 2;
}
