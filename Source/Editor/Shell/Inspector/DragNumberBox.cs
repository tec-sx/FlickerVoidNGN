using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace FlickerVoid.Editor.Inspector;

/// <summary>
/// A number box edited by dragging sideways across it (Shift for fine steps, Ctrl for coarse ones) or, after a click, by typing;
/// a click or right-click selects the whole value, so Ctrl+C or the context menu copies it. An optional coloured letter on its left
/// names the axis it edits, and a number with a range is filled up to where its value sits in it.
/// </summary>
public sealed class DragNumberBox : TextBox
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<DragNumberBox, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> StepProperty = AvaloniaProperty.Register<DragNumberBox, double>(nameof(Step), 0.05);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<DragNumberBox, double>(nameof(Minimum), double.NegativeInfinity);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<DragNumberBox, double>(nameof(Maximum), double.PositiveInfinity);

    public static readonly StyledProperty<IBrush?> AxisBrushProperty = AvaloniaProperty.Register<DragNumberBox, IBrush?>(nameof(AxisBrush));

    public static readonly StyledProperty<string?> AxisProperty = AvaloniaProperty.Register<DragNumberBox, string?>(nameof(Axis));

    /// <summary>Raised when a drag starts changing the value, so the window can record the drag as one undo step.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragStartedEvent =
        RoutedEvent.Register<DragNumberBox, RoutedEventArgs>("DragStarted", RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> DragCompletedEvent =
        RoutedEvent.Register<DragNumberBox, RoutedEventArgs>("DragCompleted", RoutingStrategies.Bubble);

    private const double DragThreshold = 3;
    private const double FineFactor = 0.1;
    private const double CoarseFactor = 10;

    private const string TrackBrushKey = "EditorDeepBrush";
    private const string FillBrushKey = "EditorNumberFillBrush";
    private const string EdgeBrushKey = "EditorAccentLineBrush";

    private readonly TextBlock _axisLetter = new() { Classes = { "axis" }, IsVisible = false };
    private Point? _pressedAt;
    private double _pressedValue;
    private bool _dragging;

    /// <summary>Whether a click has opened the box for typing; until it loses focus, the mouse selects text instead of dragging.</summary>
    private bool _typing;

    public DragNumberBox()
    {
        Classes.Add("drag");
        InnerLeftContent = _axisLetter;
        ShowValue();
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>How much the value changes for each pixel the mouse drags.</summary>
    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? AxisBrush
    {
        get => GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    /// <summary>The letter of the axis it edits (X, Y, Z), drawn in <see cref="AxisBrush"/> before the number.</summary>
    public string? Axis
    {
        get => GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ShowFill();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ShowFill();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            ShowValue();
            ShowFill();
        }
        else if (change.Property == AxisBrushProperty)
        {
            _axisLetter.Foreground = AxisBrush;
        }
        else if (change.Property == AxisProperty)
        {
            _axisLetter.Text = Axis;
            _axisLetter.IsVisible = !string.IsNullOrEmpty(Axis);
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            ShowFill();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(this);
        if (!_typing && point.Properties.IsRightButtonPressed)
        {
            base.OnPointerPressed(e);
            StartTyping();
            return;
        }

        if (_typing || IsReadOnly || !point.Properties.IsLeftButtonPressed)
        {
            base.OnPointerPressed(e);
            return;
        }

        _pressedAt = point.Position;
        _pressedValue = Value;
        _dragging = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_pressedAt is not { } start)
        {
            base.OnPointerMoved(e);
            return;
        }

        Point now = e.GetPosition(this);
        double moved = now.X - start.X;
        if (!_dragging && Math.Abs(moved) < DragThreshold)
        {
            return;
        }

        if (!_dragging)
        {
            _dragging = true;
            RoutedEventArgs started = new(DragStartedEvent);
            RaiseEvent(started);
        }

        double step = Step * GetStepFactor(e.KeyModifiers);
        double dragged = _pressedValue + (moved * step);
        Value = Math.Clamp(dragged, Minimum, Maximum);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_pressedAt is null)
        {
            base.OnPointerReleased(e);
            return;
        }

        bool clicked = !_dragging;
        _pressedAt = null;
        EndDrag();
        e.Pointer.Capture(null);
        e.Handled = true;
        if (clicked)
        {
            StartTyping();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control && string.IsNullOrEmpty(SelectedText))
        {
            SelectAll();
        }

        switch (e.Key)
        {
            case Key.Enter:
                Commit();
                e.Handled = true;
                return;
            case Key.Escape:
                ShowValue();
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetTyping(false);
        Commit();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressedAt = null;
        EndDrag();
    }

    /// <summary>
    /// Fills the box up to where the value sits between a finite minimum and maximum, with a 1 px edge: the background is one
    /// brush with hard stops, as the text box's template has no layer of its own behind the text.
    /// </summary>
    private void ShowFill()
    {
        double width = Bounds.Width;
        bool ranged = double.IsFinite(Minimum) && double.IsFinite(Maximum) && Maximum > Minimum;
        if (!ranged || width <= 0)
        {
            return;
        }

        if (!TryFindColor(TrackBrushKey, out Color track) || !TryFindColor(FillBrushKey, out Color fill) || !TryFindColor(EdgeBrushKey, out Color edge))
        {
            return;
        }

        double share = Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);
        double edgeEnd = Math.Min(share + (1 / width), 1);
        LinearGradientBrush brush = new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        brush.GradientStops.Add(new GradientStop(fill, 0));
        brush.GradientStops.Add(new GradientStop(fill, share));
        brush.GradientStops.Add(new GradientStop(edge, share));
        brush.GradientStops.Add(new GradientStop(edge, edgeEnd));
        brush.GradientStops.Add(new GradientStop(track, edgeEnd));
        brush.GradientStops.Add(new GradientStop(track, 1));
        Background = brush;
    }

    private bool TryFindColor(string key, out Color color)
    {
        if (this.TryFindResource(key, out object? found) && found is ISolidColorBrush brush)
        {
            color = brush.Color;
            return true;
        }

        color = default;
        return false;
    }

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        RoutedEventArgs completed = new(DragCompletedEvent);
        RaiseEvent(completed);
    }

    private static double GetStepFactor(KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            return FineFactor;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            return CoarseFactor;
        }

        return 1;
    }

    /// <summary>Opens the box for typing with the whole value selected, so it can be retyped, copied or pasted over.</summary>
    private void StartTyping()
    {
        SetTyping(true);
        Focus();
        SelectAll();
    }

    private void SetTyping(bool typing)
    {
        _typing = typing;
        Classes.Set("typing", typing);
    }

    /// <summary>Takes the typed number, or shows the value again when the text isn't one.</summary>
    private void Commit()
    {
        if (double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed))
        {
            Value = Math.Clamp(typed, Minimum, Maximum);
        }

        ShowValue();
    }

    private void ShowValue()
    {
        Text = Value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
