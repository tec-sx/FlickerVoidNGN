using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Mix;

/// <summary>A horizontal gain bar with its value in decibels on it: drag to set, double-click for 0 dB.</summary>
public sealed class GainBar : Control
{
    public static readonly StyledProperty<double> DecibelsProperty =
        AvaloniaProperty.Register<GainBar, double>(nameof(Decibels), defaultBindingMode: BindingMode.TwoWay);

    private const double Lowest = -30;
    private const double Highest = 6;

    private bool _dragging;

    static GainBar()
    {
        AffectsRender<GainBar>(DecibelsProperty);
        FocusableProperty.OverrideDefaultValue<GainBar>(true);
    }

    public double Decibels
    {
        get => GetValue(DecibelsProperty);
        set => SetValue(DecibelsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        IBrush field = DecibelScale.Brush(this, "EditorDeepBrush", Brushes.Black);
        IBrush fill = DecibelScale.Brush(this, "EditorNumberFillBrush", Brushes.SaddleBrown);
        IBrush text = DecibelScale.Brush(this, "EditorTextBrush", Brushes.White);
        IBrush muted = DecibelScale.Brush(this, "EditorMutedTextBrush", Brushes.Gray);
        Rect all = new(Bounds.Size);
        context.DrawRectangle(field, null, all, 3, 3);
        double width = all.Width * DecibelScale.ToPosition(Decibels, Lowest, Highest);
        context.DrawRectangle(fill, null, new Rect(0, 0, width, all.Height), 3, 3);
        string value = "-inf";
        if (Decibels > DecibelScale.Floor)
        {
            value = Decibels.ToString("0.0", CultureInfo.InvariantCulture);
        }

        FormattedText number = DecibelScale.Text(value, 11, text);
        context.DrawText(number, new Point(5, (all.Height - 13) / 2));
        FormattedText unit = DecibelScale.Text("dB", 10, muted);
        context.DrawText(unit, new Point(all.Width - unit.Width - 5, (all.Height - 12) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs args)
    {
        base.OnPointerPressed(args);
        if (args.ClickCount == 2)
        {
            Decibels = 0;
            return;
        }

        _dragging = true;
        args.Pointer.Capture(this);
        Set(args.GetPosition(this).X);
        args.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_dragging)
        {
            Set(args.GetPosition(this).X);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs args)
    {
        base.OnPointerReleased(args);
        _dragging = false;
        args.Pointer.Capture(null);
    }

    private void Set(double x)
    {
        double along = Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1);
        double decibels = Lowest + (along * (Highest - Lowest));
        decibels = Math.Round(decibels * 2) / 2;
        if (decibels <= Lowest)
        {
            decibels = DecibelScale.Floor;
        }

        Decibels = decibels;
    }
}
