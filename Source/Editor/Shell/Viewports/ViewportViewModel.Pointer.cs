using System.Numerics;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    /// <summary>A left click without Alt, which the open mode takes.</summary>
    public void PointerPressed(Vector2 pixel, int width, int height)
    {
        if (!PressBuilding(pixel, width, height))
        {
            Mode.PointerPressed(pixel, width, height);
        }
    }

    /// <summary>A left click in the creator's mesh sets a pending tool where the mouse put it (Apply keeps it), else selects: Shift adds to the selection, Ctrl selects the loop through what is hit.</summary>
    public void PointerPressed(Vector2 pixel, int width, int height, bool additive, bool loop)
    {
        if (PressBuilding(pixel, width, height) || MeshTools.Settle())
        {
            return;
        }

        if (Creator is { MeshSelection: not null } creator)
        {
            if (!creator.BeginGizmoDrag(pixel, width, height))
            {
                creator.PressMesh(pixel, width, height, additive, loop);
            }

            return;
        }

        Mode.PointerPressed(pixel, width, height);
    }

    /// <summary>Selects what is under the mouse in the scene: an icon first, then a mesh, or nothing; the game view has no icons.</summary>
    public void SelectAt(Vector2 pixel, int width, int height) => SceneMode.SelectAt(pixel, width, height);

    public void PointerMoved(Vector2 pixel, int width, int height)
    {
        MeshTools.Point(pixel, width, height);
        Mode.PointerMoved(pixel, width, height);
    }

    public void PointerLeft() => Mode.PointerLeft();

    public void PointerReleased() => Mode.PointerReleased();
}
