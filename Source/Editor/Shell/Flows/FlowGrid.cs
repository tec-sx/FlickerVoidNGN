using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Flows;

/// <summary>Faint lines under the graph every few canvas units, moving with the view, so panning and zooming show.</summary>
public sealed class FlowGrid : Control
{
    public static readonly StyledProperty<FlowCanvasView?> ViewProperty =
        AvaloniaProperty.Register<FlowGrid, FlowCanvasView?>(nameof(View));

    /// <summary>Canvas units between lines, and how many lines between stronger ones.</summary>
    private const double Spacing = 20;
    private const int MajorEvery = 8;

    private static readonly Pen MinorPen = new(new SolidColorBrush(Color.Parse("#1D1610")), 1);
    private static readonly Pen MajorPen = new(new SolidColorBrush(Color.Parse("#2A2119")), 1);

    private FlowCanvasView? _watched;

    public FlowCanvasView? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (View is not { } view)
        {
            return;
        }

        double step = Spacing * view.Zoom;
        if (step < 6)
        {
            step *= MajorEvery;
        }

        DrawLines(context, view.OffsetX, step, Bounds.Width, true);
        DrawLines(context, view.OffsetY, step, Bounds.Height, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ViewProperty)
        {
            return;
        }

        if (_watched is not null)
        {
            _watched.PropertyChanged -= HandleViewChanged;
        }

        _watched = View;
        if (_watched is not null)
        {
            _watched.PropertyChanged += HandleViewChanged;
        }

        InvalidateVisual();
    }

    private void DrawLines(DrawingContext context, double offset, double step, double length, bool vertical)
    {
        double first = offset - (Math.Ceiling(offset / step) * step);
        long index = (long)Math.Round((first - offset) / step);
        for (double at = first; at < length; at += step, index++)
        {
            Pen pen = index % MajorEvery == 0 ? MajorPen : MinorPen;
            if (vertical)
            {
                context.DrawLine(pen, new Point(at, 0), new Point(at, Bounds.Height));
            }
            else
            {
                context.DrawLine(pen, new Point(0, at), new Point(Bounds.Width, at));
            }
        }
    }

    private void HandleViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }
}
