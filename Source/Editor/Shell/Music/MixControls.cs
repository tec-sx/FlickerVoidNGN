using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace FlickerVoid.Editor.Music;

/// <summary>Draws and reads the decibel scales the music editor's meters, gain bars and faders share.</summary>
internal static class DecibelScale
{
    public const double Floor = -60;

    public static double ToPosition(double decibels, double lowest, double highest)
    {
        double clamped = Math.Clamp(decibels, lowest, highest);
        return (clamped - lowest) / (highest - lowest);
    }

    public static double FromLevel(double level)
    {
        return level <= 0.0001 ? Floor : 20 * Math.Log10(level);
    }

    public static IBrush Brush(Control control, string key, IBrush fallback)
    {
        if (control.TryFindResource(key, control.ActualThemeVariant, out object? value) && value is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }

    public static FormattedText Text(string text, double size, IBrush brush)
    {
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
    }
}

/// <summary>A level meter: a bar that rises with the loudest sample lately, green, then yellow, then red as it nears full scale.</summary>
public sealed class LevelMeter : Control
{
    public static readonly StyledProperty<double> LevelProperty = AvaloniaProperty.Register<LevelMeter, double>(nameof(Level));

    public static readonly StyledProperty<bool> VerticalProperty = AvaloniaProperty.Register<LevelMeter, bool>(nameof(Vertical));

    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#9BCB3C"));
    private static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#E0C02C"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#E0553D"));

    static LevelMeter()
    {
        AffectsRender<LevelMeter>(LevelProperty, VerticalProperty);
    }

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool Vertical
    {
        get => GetValue(VerticalProperty);
        set => SetValue(VerticalProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        IBrush deep = DecibelScale.Brush(this, "EditorDeepBrush", Brushes.Black);
        context.FillRectangle(deep, new Rect(Bounds.Size));
        double position = DecibelScale.ToPosition(DecibelScale.FromLevel(Level), -48, 0);
        double total = Vertical ? Bounds.Height : Bounds.Width;
        double lit = total * position;
        int segments = (int)(total / 3);
        for (int index = 0; index < segments; index++)
        {
            double from = index * 3.0;
            if (from + 2 > lit)
            {
                break;
            }

            double along = from / total;
            IBrush brush = along > 0.92 ? Red : along > 0.75 ? Yellow : Green;
            Rect cell = Vertical
                ? new Rect(0, Bounds.Height - from - 2, Bounds.Width, 2)
                : new Rect(from, 0, 2, Bounds.Height);
            context.FillRectangle(brush, cell);
        }
    }
}

/// <summary>A horizontal gain bar with its value in decibels on it: drag to set, double-click for 0 dB.</summary>
public sealed class GainBar : Control
{
    public static readonly StyledProperty<double> DecibelsProperty =
        AvaloniaProperty.Register<GainBar, double>(nameof(Decibels), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

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
        string value = Decibels <= DecibelScale.Floor ? "-inf" : Decibels.ToString("0.0", CultureInfo.InvariantCulture);
        context.DrawText(DecibelScale.Text(value, 11, text), new Point(5, (all.Height - 13) / 2));
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
        Decibels = decibels <= Lowest ? DecibelScale.Floor : decibels;
    }
}

/// <summary>A vertical fader on a decibel scale, +6 at the top: drag the cap, double-click for 0 dB.</summary>
public sealed class Fader : Control
{
    public static readonly StyledProperty<double> DecibelsProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(Decibels), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<Fader, double>(nameof(Maximum), 6);

    private const double Lowest = -48;
    private const double Highest = 6;

    private static readonly double[] Marks = [6, 0, -6, -12, -24];

    private bool _dragging;

    static Fader()
    {
        AffectsRender<Fader>(DecibelsProperty);
    }

    public double Decibels
    {
        get => GetValue(DecibelsProperty);
        set => SetValue(DecibelsProperty, value);
    }

    /// <summary>The highest value the cap sets; 0 for a bus, whose volume is not boosted.</summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private double Top => 8;

    private double Bottom => Bounds.Height - 8;

    public override void Render(DrawingContext context)
    {
        IBrush muted = DecibelScale.Brush(this, "EditorHintBrush", Brushes.Gray);
        IBrush line = DecibelScale.Brush(this, "EditorLineBrush", Brushes.Gray);
        IBrush cap = DecibelScale.Brush(this, "EditorMutedTextBrush", Brushes.LightGray);
        double middle = Bounds.Width / 2;
        context.DrawLine(new Pen(line, 3), new Point(middle, Top), new Point(middle, Bottom));
        foreach (double mark in Marks)
        {
            double y = Bottom - (DecibelScale.ToPosition(mark, Lowest, Highest) * (Bottom - Top));
            FormattedText label = DecibelScale.Text(mark > 0 ? $"+{mark:0}" : $"{mark:0}", 9, muted);
            context.DrawText(label, new Point(Bounds.Width - label.Width - 2, y - 6));
        }

        double knob = Bottom - (DecibelScale.ToPosition(Decibels, Lowest, Highest) * (Bottom - Top));
        context.DrawRectangle(cap, null, new Rect(middle - 11, knob - 4, 22, 8), 2, 2);
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
        Set(args.GetPosition(this).Y);
    }

    protected override void OnPointerMoved(PointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_dragging)
        {
            Set(args.GetPosition(this).Y);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs args)
    {
        base.OnPointerReleased(args);
        _dragging = false;
        args.Pointer.Capture(null);
    }

    private void Set(double y)
    {
        double along = Math.Clamp((Bottom - y) / (Bottom - Top), 0, 1);
        double decibels = Lowest + (along * (Highest - Lowest));
        decibels = Math.Round(decibels * 2) / 2;
        decibels = Math.Min(decibels, Maximum);
        Decibels = decibels <= Lowest ? DecibelScale.Floor : decibels;
    }
}
