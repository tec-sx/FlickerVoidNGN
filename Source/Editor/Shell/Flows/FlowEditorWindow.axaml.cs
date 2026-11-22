using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Flows;

/// <summary>
/// The flow graph editor: the canvas of nodes and wires, and the selected node's values beside it. Shift+A opens the Add menu at
/// the mouse, a right-click the options for what is under it, and a wire dropped on empty space the Add menu for a node wired to it.
/// </summary>
public partial class FlowEditorWindow : Window
{
    private FlowNodeViewModel? _dragged;
    private Point _dragStart;
    private FlowPoint _nodeStart;
    private FlowPinViewModel? _wiring;
    private Point _pointer;
    private Point? _panFrom;
    private bool _panned;
    private FlowCanvasView? _view;

    /// <summary>How far the mouse may move, in pixels, for a right-click to open the options menu rather than pan.</summary>
    private const double ClickSlop = 4;

    public FlowEditorWindow()
    {
        InitializeComponent();
        Surface.AddHandler(PointerPressedEvent, HandleCanvasPressed, RoutingStrategies.Tunnel);
        Surface.PointerMoved += HandleCanvasMoved;
        Surface.PointerReleased += HandleCanvasReleased;
        Surface.PointerWheelChanged += HandleWheel;
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        Opened += HandleOpened;
    }

    private FlowGraphViewModel? Graph => DataContext as FlowGraphViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_view is not null)
        {
            _view.PropertyChanged -= HandleViewChanged;
        }

        _view = Graph?.View;
        if (_view is not null)
        {
            _view.PropertyChanged += HandleViewChanged;
            ShowView();
        }
    }

    /// <summary>Starts with every node in view.</summary>
    private void HandleOpened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(Frame, DispatcherPriority.Loaded);
    }

    private void Frame()
    {
        Graph?.FrameSelected(Surface.Bounds.Width, Surface.Bounds.Height);
    }

    private void HandleViewChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ShowView();

    private void ShowView()
    {
        if (_view is not { } view)
        {
            return;
        }

        Matrix matrix = Matrix.CreateScale(view.Zoom, view.Zoom) * Matrix.CreateTranslation(view.OffsetX, view.OffsetY);
        Canvas.RenderTransform = new MatrixTransform(matrix);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox || Graph is not { } graph)
        {
            return;
        }

        if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Shift)
        {
            ShowAddMenu(graph, _pointer, null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)
        {
            Frame();
            e.Handled = true;
        }
    }

    private void HandleWheel(object? sender, PointerWheelEventArgs e)
    {
        Point screen = e.GetPosition(Surface);
        Graph?.View.ZoomAt(screen.X, screen.Y, e.Delta.Y);
        e.Handled = true;
    }

    private void HandleCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Graph is not { } graph)
        {
            return;
        }

        Surface.Focus();
        Point point = e.GetPosition(Canvas);
        PointerPointProperties pressed = e.GetCurrentPoint(Surface).Properties;
        if (pressed.IsRightButtonPressed || pressed.IsMiddleButtonPressed)
        {
            _panFrom = e.GetPosition(Surface);
            _panned = false;
            e.Pointer.Capture(Surface);
            if (pressed.IsRightButtonPressed && FindNode(e.Source) is { } clicked && graph.FindPin(point.X, point.Y, FlowGraphViewModel.GrabRadius) is null)
            {
                graph.Select(clicked);
            }

            e.Handled = true;
            return;
        }

        FlowPinViewModel? pin = graph.FindPin(point.X, point.Y, FlowGraphViewModel.GrabRadius);
        if (pin is not null)
        {
            _wiring = pin;
            e.Pointer.Capture(Surface);
            e.Handled = true;
            return;
        }

        if (FindNode(e.Source) is { } node)
        {
            graph.Select(node);
            if (pressed.IsLeftButtonPressed)
            {
                _dragged = node;
                _dragStart = point;
                _nodeStart = new FlowPoint(node.X, node.Y);
                e.Pointer.Capture(Surface);
            }

            e.Handled = true;
            return;
        }

        graph.Select(null);
    }

    /// <summary>A right-click that didn't pan: the options for the pin, node or empty space under the mouse.</summary>
    private void ShowOptionsAt(FlowGraphViewModel graph, Point point, object? source)
    {
        if (graph.FindPin(point.X, point.Y, FlowGraphViewModel.GrabRadius) is { } pin)
        {
            ShowPinOptions(graph, pin);
        }
        else if (FindNode(source) is { } node)
        {
            ShowNodeOptions(graph, node, point);
        }
        else
        {
            graph.Select(null);
            ShowCanvasOptions(graph, point);
        }
    }

    private void HandleCanvasMoved(object? sender, PointerEventArgs e)
    {
        Point point = e.GetPosition(Canvas);
        _pointer = point;
        if (_panFrom is { } from && Graph is { } panned)
        {
            Point screen = e.GetPosition(Surface);
            Point moved = screen - from;
            if (_panned || Math.Abs(moved.X) > ClickSlop || Math.Abs(moved.Y) > ClickSlop)
            {
                _panned = true;
                panned.View.Pan(moved.X, moved.Y);
                _panFrom = screen;
            }
        }
        else if (_dragged is { } node && Graph is { } graph)
        {
            double x = _nodeStart.X + point.X - _dragStart.X;
            double y = _nodeStart.Y + point.Y - _dragStart.Y;
            graph.MoveNode(node, x, y);
        }
        else if (_wiring is { } pin && Graph is { } wired)
        {
            Point start = new(pin.Point.X, pin.Point.Y);
            Point end = point;
            if (wired.FindDropTarget(point.X, point.Y, pin) is { } target)
            {
                end = new Point(target.Point.X, target.Point.Y);
            }

            Wires.SetPending(start, end);
        }
    }

    private void HandleCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_panFrom is not null)
        {
            bool clicked = !_panned && e.InitialPressMouseButton == MouseButton.Right;
            _panFrom = null;
            e.Pointer.Capture(null);
            if (clicked && Graph is { } options)
            {
                Point at = e.GetPosition(Canvas);
                IInputElement? under = Surface.InputHitTest(e.GetPosition(Surface));
                ShowOptionsAt(options, at, under);
            }

            return;
        }

        if (_wiring is { } pin && Graph is { } graph)
        {
            Point point = e.GetPosition(Canvas);
            FlowPinViewModel? target = graph.FindDropTarget(point.X, point.Y, pin);
            Wires.SetPending(null, point);
            if (target is not null)
            {
                graph.Connect(pin, target);
            }
            else if (FindNode(Surface.InputHitTest(e.GetPosition(Surface))) is null)
            {
                ShowAddMenu(graph, point, pin);
            }
        }

        _wiring = null;
        _dragged = null;
        e.Pointer.Capture(null);
    }

    /// <summary>The searchable Add menu at a point; from a dragged pin it offers only nodes that can be wired to it, and wires them.</summary>
    private void ShowAddMenu(FlowGraphViewModel graph, Point point, FlowPinViewModel? from)
    {
        IReadOnlyList<MenuChoice> choices = graph.GetAddChoices(point.X, point.Y, from);
        SearchMenu.Show(Surface, choices, MenuGrouping.Separators);
    }

    private void ShowCanvasOptions(FlowGraphViewModel graph, Point point)
    {
        MenuChoice add = new("Add Node…", () => ShowAddMenu(graph, point, null), Icon: "Add", Gesture: EditorMenus.AddGesture);
        EditorMenus.ShowOptions(Surface, [add], MenuGrouping.None);
    }

    private void ShowNodeOptions(FlowGraphViewModel graph, FlowNodeViewModel node, Point point)
    {
        MenuChoice add = new("Add Node…", () => ShowAddMenu(graph, point, null), Icon: "Add", Gesture: EditorMenus.AddGesture);
        MenuChoice remove = new("Delete", () => graph.RemoveNode(node), node.Title, "Delete", "Delete");
        EditorMenus.ShowOptions(Surface, [add, remove], MenuGrouping.Separators);
    }

    private void ShowPinOptions(FlowGraphViewModel graph, FlowPinViewModel pin)
    {
        MenuChoice disconnect = new("Break Links", () => graph.Disconnect(pin), Icon: "Remove");
        EditorMenus.ShowOptions(Surface, [disconnect], MenuGrouping.None);
    }

    /// <summary>The node under a pointer, from anything drawn on it.</summary>
    private FlowNodeViewModel? FindNode(object? source)
    {
        Visual? visual = source as Visual;
        while (visual is not null && visual != Canvas)
        {
            if (visual is StyledElement { DataContext: FlowNodeViewModel node })
            {
                return node;
            }

            visual = visual.GetVisualParent();
        }

        return null;
    }
}
