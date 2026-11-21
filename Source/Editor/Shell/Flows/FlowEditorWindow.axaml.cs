using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Flows;

/// <summary>The flow graph editor: the canvas of nodes and wires, and the selected node's values beside it.</summary>
public partial class FlowEditorWindow : Window
{
    private FlowNodeViewModel? _dragged;
    private Point _dragStart;
    private FlowPoint _nodeStart;
    private FlowPinViewModel? _wiring;

    public FlowEditorWindow()
    {
        InitializeComponent();
        Canvas.AddHandler(PointerPressedEvent, HandleCanvasPressed, RoutingStrategies.Tunnel);
        Canvas.PointerMoved += HandleCanvasMoved;
        Canvas.PointerReleased += HandleCanvasReleased;
    }

    private FlowGraphViewModel? Graph => DataContext as FlowGraphViewModel;

    private void HandleCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Graph is not { } graph)
        {
            return;
        }

        Point point = e.GetPosition(Canvas);
        PointerPointProperties pressed = e.GetCurrentPoint(Canvas).Properties;
        object? target = FindTarget(e.Source);
        if (target is FlowPinViewModel pin)
        {
            if (pressed.IsRightButtonPressed)
            {
                graph.Disconnect(pin);
            }
            else
            {
                _wiring = pin;
                e.Pointer.Capture(Canvas);
            }

            e.Handled = true;
            return;
        }

        if (target is FlowNodeViewModel node)
        {
            graph.Select(node);
            if (pressed.IsLeftButtonPressed)
            {
                _dragged = node;
                _dragStart = point;
                _nodeStart = new FlowPoint(node.X, node.Y);
                e.Pointer.Capture(Canvas);
            }

            e.Handled = true;
            return;
        }

        graph.Select(null);
        if (pressed.IsRightButtonPressed)
        {
            ShowAddMenu(graph, point);
            e.Handled = true;
        }
    }

    private void HandleCanvasMoved(object? sender, PointerEventArgs e)
    {
        Point point = e.GetPosition(Canvas);
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
            IInputElement? hit = Canvas.InputHitTest(point);
            if (FindTarget(hit) is FlowPinViewModel other)
            {
                graph.Connect(pin, other);
            }

            Wires.SetPending(null, point);
        }

        _wiring = null;
        _dragged = null;
        e.Pointer.Capture(null);
    }

    /// <summary>Offers every node type at the clicked point.</summary>
    private void ShowAddMenu(FlowGraphViewModel graph, Point point)
    {
        ContextMenu menu = new();
        foreach (string choice in graph.NodeChoices)
        {
            MenuItem item = new() { Header = $"Add {Core.Inspector.DisplayNames.FromIdentifier(choice)}" };
            item.Click += (_, _) => graph.AddNode(choice, point.X, point.Y);
            menu.Items.Add(item);
        }

        menu.Open(Canvas);
    }

    /// <summary>The pin or node under a pointer: a pin's dot, or anything else on a node.</summary>
    private object? FindTarget(object? source)
    {
        if (source is Avalonia.Controls.Shapes.Ellipse { DataContext: FlowPinViewModel pin } ellipse && ellipse.Classes.Contains("pin"))
        {
            return pin;
        }

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
