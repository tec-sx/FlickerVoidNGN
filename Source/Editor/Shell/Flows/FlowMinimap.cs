using System.Collections.Specialized;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Flows;

/// <summary>The whole graph in a corner: its nodes as boxes and the part the window shows as a frame; a click or drag moves the view there.</summary>
public sealed class FlowMinimap : Control
{
    public static readonly StyledProperty<FlowGraphViewModel?> GraphProperty =
        AvaloniaProperty.Register<FlowMinimap, FlowGraphViewModel?>(nameof(Graph));

    public static readonly StyledProperty<Size> ViewSizeProperty =
        AvaloniaProperty.Register<FlowMinimap, Size>(nameof(ViewSize));

    private static readonly IBrush NodeBrush = new SolidColorBrush(Color.Parse("#4A3D31"));
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#F5A639"));
    private static readonly Pen FramePen = new(new SolidColorBrush(Color.Parse("#F5A639")), 1.25);
    private static readonly IBrush FrameFill = new SolidColorBrush(Color.Parse("#14F5A639"));

    /// <summary>Canvas units kept round everything shown.</summary>
    private const double Room = 120;

    private FlowGraphViewModel? _watched;
    private bool _dragging;

    static FlowMinimap()
    {
        AffectsRender<FlowMinimap>(GraphProperty, ViewSizeProperty);
    }

    public FlowGraphViewModel? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    /// <summary>The size of the canvas surface the view shows, in pixels.</summary>
    public Size ViewSize
    {
        get => GetValue(ViewSizeProperty);
        set => SetValue(ViewSizeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } graph || !TryGetMap(graph, out Rect world, out double scale, out Point origin))
        {
            return;
        }

        foreach (FlowNodeViewModel node in graph.Nodes)
        {
            IBrush brush = node.IsSelected ? SelectedBrush : NodeBrush;
            Rect box = ToMap(new Rect(node.X, node.Y, FlowNodeViewModel.Width, node.Height), world, scale, origin);
            context.DrawRectangle(brush, null, box, 1.5, 1.5);
        }

        Rect shown = GetShown(graph);
        Rect frame = ToMap(shown, world, scale, origin);
        context.DrawRectangle(FrameFill, FramePen, frame);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GraphProperty)
        {
            Watch(Graph);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragging = true;
            e.Pointer.Capture(this);
            Center(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            Center(e.GetPosition(this));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void Center(Point at)
    {
        if (Graph is not { } graph || !TryGetMap(graph, out Rect world, out double scale, out Point origin))
        {
            return;
        }

        double canvasX = world.X + ((at.X - origin.X) / scale);
        double canvasY = world.Y + ((at.Y - origin.Y) / scale);
        FlowCanvasView view = graph.View;
        double wantedX = (ViewSize.Width / 2) - (canvasX * view.Zoom);
        double wantedY = (ViewSize.Height / 2) - (canvasY * view.Zoom);
        view.Pan(wantedX - view.OffsetX, wantedY - view.OffsetY);
    }

    private Rect GetShown(FlowGraphViewModel graph)
    {
        FlowCanvasView view = graph.View;
        FlowPoint topLeft = view.ToCanvas(0, 0);
        FlowPoint bottomRight = view.ToCanvas(ViewSize.Width, ViewSize.Height);
        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    private bool TryGetMap(FlowGraphViewModel graph, out Rect world, out double scale, out Point origin)
    {
        world = default;
        scale = 1;
        origin = default;
        if (graph.Nodes.Count == 0 || Bounds.Width < 4 || Bounds.Height < 4)
        {
            return false;
        }

        double minX = graph.Nodes.Min(node => node.X);
        double minY = graph.Nodes.Min(node => node.Y);
        double maxX = graph.Nodes.Max(node => node.X + FlowNodeViewModel.Width);
        double maxY = graph.Nodes.Max(node => node.Y + node.Height);
        world = new Rect(minX - Room, minY - Room, maxX - minX + (2 * Room), maxY - minY + (2 * Room));
        scale = Math.Min(Bounds.Width / world.Width, Bounds.Height / world.Height);
        origin = new Point((Bounds.Width - (world.Width * scale)) / 2, (Bounds.Height - (world.Height * scale)) / 2);
        return true;
    }

    private static Rect ToMap(Rect box, Rect world, double scale, Point origin)
    {
        return new Rect(origin.X + ((box.X - world.X) * scale), origin.Y + ((box.Y - world.Y) * scale), box.Width * scale, box.Height * scale);
    }

    private void Watch(FlowGraphViewModel? graph)
    {
        if (_watched is not null)
        {
            _watched.Nodes.CollectionChanged -= HandleNodesChanged;
            foreach (FlowNodeViewModel node in _watched.Nodes)
            {
                node.PropertyChanged -= HandleChanged;
            }

            _watched.View.PropertyChanged -= HandleChanged;
            _watched.PropertyChanged -= HandleChanged;
        }

        _watched = graph;
        if (graph is not null)
        {
            graph.Nodes.CollectionChanged += HandleNodesChanged;
            foreach (FlowNodeViewModel node in graph.Nodes)
            {
                node.PropertyChanged += HandleChanged;
            }

            graph.View.PropertyChanged += HandleChanged;
            graph.PropertyChanged += HandleChanged;
        }
    }

    private void HandleNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (FlowNodeViewModel node in e.OldItems?.OfType<FlowNodeViewModel>() ?? [])
        {
            node.PropertyChanged -= HandleChanged;
        }

        foreach (FlowNodeViewModel node in e.NewItems?.OfType<FlowNodeViewModel>() ?? [])
        {
            node.PropertyChanged += HandleChanged;
        }

        InvalidateVisual();
    }

    private void HandleChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
}
