using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace FlickerVoid.Editor.Controls;

/// <summary>The top edge of a docked panel (the content drawer, the Day dock): a line that drags the panel's height.</summary>
public sealed class HeightGrip : Border
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<HeightGrip, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<HeightGrip, double>(nameof(Minimum), 120);

    /// <summary>The most of the window's height the panel may take.</summary>
    private const double LargestShare = 0.75;

    private double _startValue;

    private double _startY;

    private bool _dragging;

    public HeightGrip()
    {
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
    }

    /// <summary>The panel's height, changed while the grip is dragged.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        TopLevel? top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        _startValue = Value;
        _startY = e.GetPosition(top).Y;
        _dragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        TopLevel? top = TopLevel.GetTopLevel(this);
        if (!_dragging || top is null)
        {
            return;
        }

        double y = e.GetPosition(top).Y;
        double largest = Math.Max(Minimum, top.Bounds.Height * LargestShare);
        Value = Math.Clamp(_startValue + _startY - y, Minimum, largest);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }
}
