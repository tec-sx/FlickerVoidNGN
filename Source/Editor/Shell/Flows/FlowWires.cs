using System.Collections.Specialized;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Flows;

/// <summary>
/// Draws the graph's wires as curves from output to input and the wire being dragged from a pin, or, in pin mode, the rings of every
/// pin, which stand over the nodes so the half of them that hangs over a node's edge shows.
/// </summary>
public sealed class FlowWires : Control
{
    public static readonly StyledProperty<FlowGraphViewModel?> GraphProperty =
        AvaloniaProperty.Register<FlowWires, FlowGraphViewModel?>(nameof(Graph));

    public static readonly StyledProperty<bool> DrawsPinsProperty =
        AvaloniaProperty.Register<FlowWires, bool>(nameof(DrawsPins));

    public static readonly StyledProperty<FlowPinViewModel?> HoveredPinProperty =
        AvaloniaProperty.Register<FlowWires, FlowPinViewModel?>(nameof(HoveredPin));

    private static readonly Pen RingPen = new(new SolidColorBrush(Color.Parse("#B6AB99")), 1.5);
    private static readonly Pen OutputRingPen = new(new SolidColorBrush(Color.Parse("#EDE5D8")), 1.5);
    private static readonly Pen HoverRingPen = new(new SolidColorBrush(Color.Parse("#F5A639")), 1.75);
    private static readonly IBrush RingFill = new SolidColorBrush(Color.Parse("#1D1610"));
    private static readonly Pen WirePen = new(new SolidColorBrush(Color.Parse("#8F8372")), 1.75);
    private static readonly Pen BackWirePen = new(new SolidColorBrush(Color.Parse("#8F8372")), 1.75) { DashStyle = new DashStyle([4, 4], 0) };
    private static readonly Pen ActivePen = new(new SolidColorBrush(Color.Parse("#F5A639")), 2.25);
    private static readonly Pen ActiveBackPen = new(new SolidColorBrush(Color.Parse("#F5A639")), 2.25) { DashStyle = new DashStyle([4, 4], 0) };
    private static readonly Pen PendingPen = new(new SolidColorBrush(Color.Parse("#F5A639")), 2);

    private FlowGraphViewModel? _watched;
    private (Point Start, Point End)? _pending;

    static FlowWires()
    {
        AffectsRender<FlowWires>(GraphProperty, DrawsPinsProperty, HoveredPinProperty);
    }

    public FlowGraphViewModel? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    /// <summary>Whether this layer draws the pins' rings instead of the wires.</summary>
    public bool DrawsPins
    {
        get => GetValue(DrawsPinsProperty);
        set => SetValue(DrawsPinsProperty, value);
    }

    /// <summary>The pin under the mouse, drawn larger and in the accent colour.</summary>
    public FlowPinViewModel? HoveredPin
    {
        get => GetValue(HoveredPinProperty);
        set => SetValue(HoveredPinProperty, value);
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
        if (DrawsPins)
        {
            DrawPins(context);
            return;
        }

        if (Graph is { } graph)
        {
            foreach (FlowLinkViewModel link in graph.Links)
            {
                Point start = new(link.Start.X, link.Start.Y);
                Point end = new(link.End.X, link.End.Y);
                bool back = end.X < start.X;
                bool active = graph.Selected is { } selected && link.Touches(selected);
                Pen pen = SelectPen(back, active);
                DrawWire(context, pen, start, end);
            }
        }

        if (_pending is { } pending)
        {
            DrawWire(context, PendingPen, pending.Start, pending.End);
        }
    }

    private void DrawPins(DrawingContext context)
    {
        if (Graph is not { } graph)
        {
            return;
        }

        foreach (FlowNodeViewModel node in graph.Nodes)
        {
            foreach (FlowPinViewModel pin in node.Inputs.Concat(node.Outputs))
            {
                bool hovered = pin == HoveredPin;
                Pen pen = pin.IsOutput ? OutputRingPen : RingPen;
                if (hovered)
                {
                    pen = HoverRingPen;
                }

                double radius = hovered ? 6.5 : 5;
                context.DrawEllipse(RingFill, pen, new Point(pin.Point.X, pin.Point.Y), radius, radius);
            }
        }
    }

    private static Pen SelectPen(bool back, bool active)
    {
        if (active)
        {
            return back ? ActiveBackPen : ActivePen;
        }

        return back ? BackWirePen : WirePen;
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
            _watched.PropertyChanged -= HandleGraphChanged;
            _watched.Links.CollectionChanged -= HandleLinksChanged;
            _watched.Nodes.CollectionChanged -= HandleNodesChanged;
            foreach (FlowNodeViewModel node in _watched.Nodes)
            {
                node.PropertyChanged -= HandleLinkChanged;
            }

            foreach (FlowLinkViewModel link in _watched.Links)
            {
                link.PropertyChanged -= HandleLinkChanged;
            }
        }

        _watched = graph;
        if (graph is not null)
        {
            graph.PropertyChanged += HandleGraphChanged;
            graph.Links.CollectionChanged += HandleLinksChanged;
            graph.Nodes.CollectionChanged += HandleNodesChanged;
            foreach (FlowNodeViewModel node in graph.Nodes)
            {
                node.PropertyChanged += HandleLinkChanged;
            }

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

    private void HandleNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (FlowNodeViewModel node in e.OldItems?.OfType<FlowNodeViewModel>() ?? [])
        {
            node.PropertyChanged -= HandleLinkChanged;
        }

        foreach (FlowNodeViewModel node in e.NewItems?.OfType<FlowNodeViewModel>() ?? [])
        {
            node.PropertyChanged += HandleLinkChanged;
        }

        InvalidateVisual();
    }

    private void HandleGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowGraphViewModel.Selected))
        {
            InvalidateVisual();
        }
    }

    private void HandleLinkChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }
}
