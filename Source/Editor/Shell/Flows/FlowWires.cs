using System.Collections.Specialized;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Flows;

/// <summary>Draws the graph's wires as curves from output to input, and the wire being dragged from a pin.</summary>
public sealed class FlowWires : Control
{
    public static readonly StyledProperty<FlowGraphViewModel?> GraphProperty =
        AvaloniaProperty.Register<FlowWires, FlowGraphViewModel?>(nameof(Graph));

    private static readonly Pen WirePen = new(new SolidColorBrush(Color.Parse("#C8C2B0")), 2);
    private static readonly Pen PendingPen = new(new SolidColorBrush(Color.Parse("#E0AC2C")), 2);

    private FlowGraphViewModel? _watched;
    private (Point Start, Point End)? _pending;

    static FlowWires()
    {
        AffectsRender<FlowWires>(GraphProperty);
    }

    public FlowGraphViewModel? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    /// <summary>Shows the wire being dragged, or none.</summary>
    public void SetPending(Point? start, Point end)
    {
        if (start is { } from)
        {
            _pending = (from, end);
        }
        else
        {
            _pending = null;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Graph is { } graph)
        {
            foreach (FlowLinkViewModel link in graph.Links)
            {
                Point start = new(link.Start.X, link.Start.Y);
                Point end = new(link.End.X, link.End.Y);
                DrawWire(context, WirePen, start, end);
            }
        }

        if (_pending is { } pending)
        {
            DrawWire(context, PendingPen, pending.Start, pending.End);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GraphProperty)
        {
            Watch(Graph);
        }
    }

    private static void DrawWire(DrawingContext context, Pen pen, Point start, Point end)
    {
        double bend = Math.Max(40, Math.Abs(end.X - start.X) / 2);
        StreamGeometry geometry = new();
        using (StreamGeometryContext figure = geometry.Open())
        {
            figure.BeginFigure(start, false);
            Point first = new(start.X + bend, start.Y);
            Point second = new(end.X - bend, end.Y);
            figure.CubicBezierTo(first, second, end);
            figure.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private void Watch(FlowGraphViewModel? graph)
    {
        if (_watched is not null)
        {
            _watched.Links.CollectionChanged -= HandleLinksChanged;
            foreach (FlowLinkViewModel link in _watched.Links)
            {
                link.PropertyChanged -= HandleLinkChanged;
            }
        }

        _watched = graph;
        if (graph is not null)
        {
            graph.Links.CollectionChanged += HandleLinksChanged;
            foreach (FlowLinkViewModel link in graph.Links)
            {
                link.PropertyChanged += HandleLinkChanged;
            }
        }
    }

    private void HandleLinksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (FlowLinkViewModel link in e.OldItems?.OfType<FlowLinkViewModel>() ?? [])
        {
            link.PropertyChanged -= HandleLinkChanged;
        }

        foreach (FlowLinkViewModel link in e.NewItems?.OfType<FlowLinkViewModel>() ?? [])
        {
            link.PropertyChanged += HandleLinkChanged;
        }

        InvalidateVisual();
    }

    private void HandleLinkChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }
}
