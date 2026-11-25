using System.Numerics;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    /// <summary>A left click without Alt, which the open mode takes.</summary>
    public void PointerPressed(Vector2 pixel, int width, int height) => Mode.PointerPressed(pixel, width, height);

    /// <summary>Selects what is under the mouse in the scene: an icon first, then a mesh, or nothing; the game view has no icons.</summary>
    public void SelectAt(Vector2 pixel, int width, int height) => SceneMode.SelectAt(pixel, width, height);

    public void PointerMoved(Vector2 pixel, int width, int height) => Mode.PointerMoved(pixel, width, height);

    public void PointerReleased() => Mode.PointerReleased();
}
