using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Flows;

/// <summary>Faint dots under the graph every few canvas units, moving with the view, so panning and zooming show.</summary>
public sealed class FlowGrid : Control
{
    public static readonly StyledProperty<FlowCanvasView?> ViewProperty =
        AvaloniaProperty.Register<FlowGrid, FlowCanvasView?>(nameof(View));

    /// <summary>Canvas units between lines, and how many lines between stronger ones.</summary>
    private const double Spacing = 20;
    private const int MajorEvery = 8;

    private static readonly IBrush DotBrush = new SolidColorBrush(Color.Parse("#2E241B"));
    private static readonly IBrush MajorDotBrush = new SolidColorBrush(Color.Parse("#3A2F26"));

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

        double firstX = view.OffsetX - (Math.Ceiling(view.OffsetX / step) * step);
        double firstY = view.OffsetY - (Math.Ceiling(view.OffsetY / step) * step);
        long column = (long)Math.Round((firstX - view.OffsetX) / step);
        for (double x = firstX; x < Bounds.Width; x += step, column++)
        {
            long row = (long)Math.Round((firstY - view.OffsetY) / step);
            for (double y = firstY; y < Bounds.Height; y += step, row++)
            {
                bool major = column % MajorEvery == 0 && row % MajorEvery == 0;
                IBrush brush = major ? MajorDotBrush : DotBrush;
                context.DrawRectangle(brush, null, new Rect(x - 1, y - 1, 2, 2));
            }
        }
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

    private void HandleViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }
}
