using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FlickerVoid.Interface.Controls;

/// <summary>
/// A HUD panel's frame: a see-through fill inside a thin outline with two cut corners and short ticks at the others, drawn in one
/// stroke brush, white by default so the theme's tint colours every frame at once.
/// </summary>
public sealed class HudFrame : Decorator
{
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<HudFrame, IBrush?>(nameof(Stroke), Brushes.White);

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<HudFrame, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> ChamferProperty = AvaloniaProperty.Register<HudFrame, double>(nameof(Chamfer), 10);

    static HudFrame()
    {
        AffectsRender<HudFrame>(StrokeProperty, FillProperty, ChamferProperty);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>How far the cut corners (top left and bottom right) reach along each side.</summary>
    public double Chamfer
    {
        get => GetValue(ChamferProperty);
        set => SetValue(ChamferProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        double cut = Math.Min(Chamfer, Math.Min(bounds.Width, bounds.Height) / 2);
        double left = 0.5;
        double top = 0.5;
        double right = bounds.Width - 0.5;
        double bottom = bounds.Height - 0.5;
        StreamGeometry outline = new();
        using (StreamGeometryContext path = outline.Open())
        {
            path.BeginFigure(new Point(left + cut, top), true);
            path.LineTo(new Point(right, top));
            path.LineTo(new Point(right, bottom - cut));
            path.LineTo(new Point(right - cut, bottom));
            path.LineTo(new Point(left, bottom));
            path.LineTo(new Point(left, top + cut));
            path.EndFigure(true);
        }

        Pen edge = new(Stroke, 1, lineCap: PenLineCap.Square);
        context.DrawGeometry(Fill, edge, outline);

        // Ticks just outside the two square corners, a little heavier than the outline.
        Pen tick = new(Stroke, 2, lineCap: PenLineCap.Square);
        double reach = cut;
        context.DrawLine(tick, new Point(right - reach, top - 2), new Point(right + 2, top - 2));
        context.DrawLine(tick, new Point(right + 2, top - 2), new Point(right + 2, top + reach));
        context.DrawLine(tick, new Point(left - 2, bottom - reach), new Point(left - 2, bottom + 2));
        context.DrawLine(tick, new Point(left - 2, bottom + 2), new Point(left + reach, bottom + 2));
    }
}
