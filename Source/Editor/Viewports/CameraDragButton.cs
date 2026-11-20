using System.Numerics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using FlickerVoid.Engine.Editor.Cameras;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>What dragging a <see cref="CameraDragButton"/> does to the editor camera.</summary>
public enum CameraDrag
{
    Pan,
    Zoom,
}

/// <summary>Blender's hand and magnifier buttons beside the navigation gizmo: dragging one pans or zooms the editor camera.</summary>
public sealed class CameraDragButton : Button
{
    public static readonly StyledProperty<EditorCamera?> CameraProperty =
        AvaloniaProperty.Register<CameraDragButton, EditorCamera?>(nameof(Camera));

    public static readonly StyledProperty<CameraDrag> DragProperty =
        AvaloniaProperty.Register<CameraDragButton, CameraDrag>(nameof(Drag));

    /// <summary>The height of the viewport the camera draws, in the same units as the drag, so a pan follows the mouse.</summary>
    public static readonly StyledProperty<double> ViewportHeightProperty =
        AvaloniaProperty.Register<CameraDragButton, double>(nameof(ViewportHeight), 1);

    private const float DollyStepsPerPixel = 0.03f;

    private Point? _last;

    protected override Type StyleKeyOverride => typeof(Button);

    public EditorCamera? Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public CameraDrag Drag
    {
        get => GetValue(DragProperty);
        set => SetValue(DragProperty, value);
    }

    public double ViewportHeight
    {
        get => GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _last = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_last is not { } last || Camera is not { } camera)
        {
            return;
        }

        Point position = e.GetPosition(this);
        Point delta = position - last;
        _last = position;
        if (Drag == CameraDrag.Pan)
        {
            int height = Math.Max(1, (int)ViewportHeight);
            camera.Pan(new Vector2((float)delta.X, (float)delta.Y), height);
        }
        else
        {
            camera.Dolly((float)(delta.X - delta.Y) * DollyStepsPerPixel);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _last = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _last = null;
    }
}
