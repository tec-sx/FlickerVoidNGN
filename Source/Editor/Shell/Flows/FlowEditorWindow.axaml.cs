using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    public FlowEditorWindow()
    {
        InitializeComponent();
        Canvas.AddHandler(PointerPressedEvent, HandleCanvasPressed, RoutingStrategies.Tunnel);
        Canvas.PointerMoved += HandleCanvasMoved;
        Canvas.PointerReleased += HandleCanvasReleased;
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
    }

    private FlowGraphViewModel? Graph => DataContext as FlowGraphViewModel;

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        bool addKey = e.Key == Key.A && e.KeyModifiers == KeyModifiers.Shift;
        if (!addKey || e.Source is TextBox || Graph is not { } graph)
        {
            return;
        }

        ShowAddMenu(graph, _pointer, null);
        e.Handled = true;
    }

    private void HandleCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Graph is not { } graph)
        {
            return;
        }

        Point point = e.GetPosition(Canvas);
        PointerPointProperties pressed = e.GetCurrentPoint(Canvas).Properties;
        FlowPinViewModel? pin = graph.FindPin(point.X, point.Y);
        if (pin is not null)
        {
            if (pressed.IsRightButtonPressed)
            {
                ShowPinOptions(graph, pin);
            }
            else
            {
                _wiring = pin;
                e.Pointer.Capture(Canvas);
            }

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
                e.Pointer.Capture(Canvas);
            }
            else if (pressed.IsRightButtonPressed)
            {
                ShowNodeOptions(graph, node, point);
            }

            e.Handled = true;
            return;
        }

        graph.Select(null);
        if (pressed.IsRightButtonPressed)
        {
            ShowCanvasOptions(graph, point);
            e.Handled = true;
        }
    }

    private void HandleCanvasMoved(object? sender, PointerEventArgs e)
    {
        Point point = e.GetPosition(Canvas);
        _pointer = point;
        if (_dragged is { } node && Graph is { } graph)
        {
            double x = Math.Max(0, _nodeStart.X + point.X - _dragStart.X);
            double y = Math.Max(0, _nodeStart.Y + point.Y - _dragStart.Y);
            graph.MoveNode(node, x, y);
        }
        else if (_wiring is { } pin)
        {
            Point start = new(pin.Point.X, pin.Point.Y);
            Wires.SetPending(start, point);
        }
    }

    private void HandleCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_wiring is { } pin && Graph is { } graph)
        {
            Point point = e.GetPosition(Canvas);
            FlowPinViewModel? other = graph.FindPin(point.X, point.Y);
            Wires.SetPending(null, point);
            if (other is not null && other != pin)
            {
                graph.Connect(pin, other);
            }
            else if (other is null && FindNode(Canvas.InputHitTest(point)) is null)
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
        SearchMenu.Show(Canvas, choices, MenuGrouping.Separators);
    }

    private void ShowCanvasOptions(FlowGraphViewModel graph, Point point)
    {
        MenuChoice add = new("Add Node…", () => ShowAddMenu(graph, point, null), Icon: "Add", Gesture: EditorMenus.AddGesture);
        EditorMenus.ShowOptions(Canvas, [add], MenuGrouping.None);
    }

    private void ShowNodeOptions(FlowGraphViewModel graph, FlowNodeViewModel node, Point point)
    {
        MenuChoice add = new("Add Node…", () => ShowAddMenu(graph, point, null), Icon: "Add", Gesture: EditorMenus.AddGesture);
        MenuChoice remove = new("Delete", () => graph.RemoveNode(node), node.Title, "Delete", "Delete");
        EditorMenus.ShowOptions(Canvas, [add, remove], MenuGrouping.Separators);
    }

    private void ShowPinOptions(FlowGraphViewModel graph, FlowPinViewModel pin)
    {
        MenuChoice disconnect = new("Break Links", () => graph.Disconnect(pin), Icon: "Remove");
        EditorMenus.ShowOptions(Canvas, [disconnect], MenuGrouping.None);
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
