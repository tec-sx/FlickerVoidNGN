using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Mix;

/// <summary>
/// A vertical fader on a decibel scale, +6 at the top: a 2 px track on the base colour and an 18 by 9 cap; drag the cap,
/// double-click for 0 dB. The scale's marks are drawn beside it by a <see cref="FaderScale"/>.
/// </summary>
public sealed class Fader : Control
{
    public const double Lowest = -48;

    public const double Highest = 6;

    /// <summary>The room above the top mark and below the bottom one, so the cap stays whole.</summary>
    public const double Ends = 8;

    public const double CapWidth = 18;

    public const double CapHeight = 9;

    public static readonly StyledProperty<double> DecibelsProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(Decibels), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<Fader, double>(nameof(Maximum), 6);

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

    private double Top => Ends;

    private double Bottom => Bounds.Height - Ends;

    /// <summary>Where a value sits from the top of a fader of the height.</summary>
    public static double ToY(double decibels, double height)
    {
        double top = Ends;
        double bottom = height - Ends;
        return bottom - (DecibelScale.ToPosition(decibels, Lowest, Highest) * (bottom - top));
    }

    public override void Render(DrawingContext context)
    {
        Color track = ThemeColors.Find(this, "EditorFrameBrush");
        Color cap = ThemeColors.Find(this, "EditorMutedTextBrush");
        double middle = Bounds.Width / 2;
        context.DrawRectangle(new SolidColorBrush(track), null, new Rect(middle - 1, Top, 2, Bottom - Top), 1, 1);
        double knob = ToY(Decibels, Bounds.Height);
        Rect capRect = new(middle - (CapWidth / 2), knob - (CapHeight / 2), CapWidth, CapHeight);
        Pen edge = new(new SolidColorBrush(track), 1);
        context.DrawRectangle(new SolidColorBrush(cap), edge, capRect, 2, 2);
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
        if (decibels <= Lowest)
        {
            decibels = DecibelScale.Floor;
        }

        Decibels = decibels;
    }
}
