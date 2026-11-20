using System.Numerics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

using FlickerVoid.Engine.Editor.Cameras;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>
/// Blender's navigation gizmo: the world axes as the editor camera sees them. Dragging it orbits the camera round its pivot, and
/// clicking an axis's ball looks along that axis at the pivot.
/// </summary>
public sealed class NavigationGizmo : Control
{
    public static readonly StyledProperty<EditorCamera?> CameraProperty =
        AvaloniaProperty.Register<NavigationGizmo, EditorCamera?>(nameof(Camera));

    private const double BallRadius = 8;
    private const float RadiansPerPixel = 0.01f;

    /// <summary>A click is a press and release that moved less than this, in device-independent pixels.</summary>
    private const double ClickSlop = 3;

    private static readonly Axis[] Axes =
    [
        new("X", Vector3.UnitX, Color.FromRgb(0xE8, 0x4A, 0x5F), true),
        new("Y", Vector3.UnitY, Color.FromRgb(0x8C, 0xC8, 0x3C), true),
        new("Z", Vector3.UnitZ, Color.FromRgb(0x3C, 0x8C, 0xE8), true),
        new("-X", -Vector3.UnitX, Color.FromRgb(0xE8, 0x4A, 0x5F), false),
        new("-Y", -Vector3.UnitY, Color.FromRgb(0x8C, 0xC8, 0x3C), false),
        new("-Z", -Vector3.UnitZ, Color.FromRgb(0x3C, 0x8C, 0xE8), false),
    ];

    private static readonly Typeface LabelFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private readonly DispatcherTimer _redraw = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private Point? _pressed;
    private Point _last;
    private bool _dragged;
    private bool _hovered;

    public NavigationGizmo()
    {
        _redraw.Tick += (_, _) => InvalidateVisual();
        ToolTip.SetTip(this, "Drag to orbit; click an axis to look along it.");
    }

    public EditorCamera? Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        Point center = bounds.Center;
        double radius = (Math.Min(bounds.Width, bounds.Height) / 2) - BallRadius;
        if (_hovered || _pressed is not null)
        {
            context.DrawEllipse(new SolidColorBrush(Colors.White, 0.12), null, center, radius + BallRadius, radius + BallRadius);
        }
        else
        {
            context.DrawEllipse(Brushes.Transparent, null, center, radius + BallRadius, radius + BallRadius);
        }

        if (Camera is not { } camera)
        {
            return;
        }

        foreach ((Axis axis, Point point, float depth) in Project(camera, center, radius).OrderBy(projected => projected.Depth))
        {
            Color color = axis.Positive ? axis.Color : Blend(axis.Color, depth);
            if (axis.Positive)
            {
                context.DrawLine(new Pen(new SolidColorBrush(color), 2), center, point);
            }

            IBrush fill = new SolidColorBrush(color, axis.Positive ? 1 : 0.55);
            context.DrawEllipse(fill, null, point, BallRadius, BallRadius);
            if (axis.Positive)
            {
                FormattedText label = new(axis.Name, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, 10, Brushes.Black);
                context.DrawText(label, new Point(point.X - (label.Width / 2), point.Y - (label.Height / 2)));
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _redraw.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _redraw.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = e.GetPosition(this);
        _last = _pressed.Value;
        _dragged = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressed is not { } pressed || Camera is not { } camera)
        {
            return;
        }

        Point position = e.GetPosition(this);
        Point fromPress = position - pressed;
        if (!_dragged && Math.Abs(fromPress.X) + Math.Abs(fromPress.Y) < ClickSlop)
        {
            return;
        }

        _dragged = true;
        Point delta = position - _last;
        _last = position;
        camera.Orbit(-(float)delta.X * RadiansPerPixel, -(float)delta.Y * RadiansPerPixel);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed is null)
        {
            return;
        }

        Point position = e.GetPosition(this);
        if (!_dragged && Camera is { } camera)
        {
            SnapToAxisAt(camera, position);
        }

        _pressed = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = null;
    }

    /// <summary>Looks at the pivot from the side of the axis whose ball is under the point, nearest the viewer first.</summary>
    private void SnapToAxisAt(EditorCamera camera, Point position)
    {
        Rect bounds = new(Bounds.Size);
        double radius = (Math.Min(bounds.Width, bounds.Height) / 2) - BallRadius;
        foreach ((Axis axis, Point point, _) in Project(camera, bounds.Center, radius).OrderByDescending(projected => projected.Depth))
        {
            Point offset = position - point;
            if ((offset.X * offset.X) + (offset.Y * offset.Y) > BallRadius * BallRadius)
            {
                continue;
            }

            Vector3 from = axis.Direction;
            float yaw = camera.Yaw;
            float pitch = 0;
            if (from.Y != 0)
            {
                pitch = from.Y > 0 ? -EditorCamera.PitchLimit : EditorCamera.PitchLimit;
            }
            else
            {
                // The camera looks along (-sin yaw, 0, -cos yaw); seen from +X it looks along -X.
                yaw = MathF.Atan2(from.X, from.Z);
            }

            camera.LookAt(camera.Pivot, yaw, pitch, camera.PivotDistance);
            return;
        }
    }

    private static IEnumerable<(Axis Axis, Point Point, float Depth)> Project(EditorCamera camera, Point center, double radius)
    {
        Vector3 right = camera.Right;
        Vector3 up = camera.Up;
        Vector3 toViewer = -camera.Forward;
        foreach (Axis axis in Axes)
        {
            double x = Vector3.Dot(axis.Direction, right) * radius;
            double y = -Vector3.Dot(axis.Direction, up) * radius;
            float depth = Vector3.Dot(axis.Direction, toViewer);
            yield return (axis, new Point(center.X + x, center.Y + y), depth);
        }
    }

    /// <summary>Darkens a negative axis's ball as it turns away from the viewer.</summary>
    private static Color Blend(Color color, float depth)
    {
        double keep = 0.6 + (0.4 * Math.Clamp(depth, -1, 1));
        return Color.FromRgb((byte)(color.R * keep), (byte)(color.G * keep), (byte)(color.B * keep));
    }

    private sealed record Axis(string Name, Vector3 Direction, Color Color, bool Positive);
}
