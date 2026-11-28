using System.Numerics;

using FlickerVoid.Editor.Core.Overlays;

namespace FlickerVoid.Editor.Core.Creators;

/// <summary>
/// A creator's own tools in the viewport, such as the building creator's plan tools: they see the mouse before the creator's parts are
/// picked, and draw over the creation.
/// </summary>
public interface ICreatorTool
{
    /// <summary>A left click; true when the tool took it, so no part is picked.</summary>
    bool Press(Vector2 pixel, int width, int height);

    void Move(Vector2 pixel, int width, int height);

    /// <summary>The mouse left the viewport, so nothing is under it.</summary>
    void Leave();

    void Release();

    void Draw(OverlayBuilder overlay);
}
