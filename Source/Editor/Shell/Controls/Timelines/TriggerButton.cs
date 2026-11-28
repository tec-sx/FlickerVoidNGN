using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// A button that triggers something on the beat (rule R28): a cue, a shot, later a camera cue. 24 px high, radius 5, a 16 px key
/// box in its colour, its icon, its name and a note. Idle it is washed 14% in its colour with a 55% border; live it is filled with
/// its colour in dark ink; queued its border is dashed 2 px with a 3 px glow that pulses on the beat.
/// </summary>
[PseudoClasses(LiveClass, QueuedClass)]
public class TriggerButton : Button
{
    public const double ButtonHeight = 24;

    public const double Radius = 5;

    public const double KeyBox = 16;

    public static readonly StyledProperty<Color> TintProperty = AvaloniaProperty.Register<TriggerButton, Color>(nameof(Tint), Colors.Gray);

    public static readonly StyledProperty<RegionState> StateProperty = AvaloniaProperty.Register<TriggerButton, RegionState>(nameof(State));

    public static readonly StyledProperty<string?> KeyProperty = AvaloniaProperty.Register<TriggerButton, string?>(nameof(Key));

    public static readonly StyledProperty<string?> NoteProperty = AvaloniaProperty.Register<TriggerButton, string?>(nameof(Note));

    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<TriggerButton, Geometry?>(nameof(Icon));

    public static readonly DirectProperty<TriggerButton, IBrush?> TintBrushProperty =
        AvaloniaProperty.RegisterDirect<TriggerButton, IBrush?>(nameof(TintBrush), button => button.TintBrush);

    public static readonly DirectProperty<TriggerButton, IBrush?> InkProperty =
        AvaloniaProperty.RegisterDirect<TriggerButton, IBrush?>(nameof(Ink), button => button.Ink);

    public static readonly DirectProperty<TriggerButton, IBrush?> KeyBoxBrushProperty =
        AvaloniaProperty.RegisterDirect<TriggerButton, IBrush?>(nameof(KeyBoxBrush), button => button.KeyBoxBrush);

    public static readonly DirectProperty<TriggerButton, Geometry?> ShownIconProperty =
        AvaloniaProperty.RegisterDirect<TriggerButton, Geometry?>(nameof(ShownIcon), button => button.ShownIcon);

    private const string LiveClass = ":live";

    private const string QueuedClass = ":queued";

    private IBrush? _tintBrush;
    private IBrush? _ink;
    private IBrush? _keyBoxBrush;
    private Geometry? _shownIcon;
    private bool _pulsing;

    static TriggerButton()
    {
        AffectsRender<TriggerButton>(TintProperty, StateProperty);
    }

    public TriggerButton()
    {
        Update();
    }

    public Color Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public RegionState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>The key that triggers it, in the box at its start, or null.</summary>
    public string? Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>A few words after the name ("playing", "queued · in 3 bars").</summary>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>The icon after the key box while idle; live shows the loop's, queued the hourglass.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IBrush? TintBrush
    {
        get => _tintBrush;
        private set => SetAndRaise(TintBrushProperty, ref _tintBrush, value);
    }

    /// <summary>The text's colour: dark ink on a live button, else the theme's text.</summary>
    public IBrush? Ink
    {
        get => _ink;
        private set => SetAndRaise(InkProperty, ref _ink, value);
    }

    public IBrush? KeyBoxBrush
    {
        get => _keyBoxBrush;
        private set => SetAndRaise(KeyBoxBrushProperty, ref _keyBoxBrush, value);
    }

    public Geometry? ShownIcon
    {
        get => _shownIcon;
        private set => SetAndRaise(ShownIconProperty, ref _shownIcon, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        Color panel = ThemeColors.Find(this, "EditorPanelBrush");
        Color tint = Tint;
        switch (State)
        {
            case RegionState.Live:
                context.DrawRectangle(new SolidColorBrush(tint), new Pen(new SolidColorBrush(tint), 1), bounds.Deflate(0.5), Radius, Radius);
                break;
            case RegionState.Queued:
                DrawQueued(context, bounds, panel, tint);
                break;
            default:
                Color fill = ThemeColors.Mix(tint, panel, 0.14);
                Color border = ThemeColors.Mix(tint, panel, 0.55);
                context.DrawRectangle(new SolidColorBrush(fill), new Pen(new SolidColorBrush(border), 1), bounds.Deflate(0.5), Radius, Radius);
                break;
        }

        base.Render(context);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TintProperty || change.Property == StateProperty || change.Property == IconProperty)
        {
            Update();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Update();
        SetPulsing(State == RegionState.Queued);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SetPulsing(false);
    }

    private void DrawQueued(DrawingContext context, Rect bounds, Color panel, Color tint)
    {
        Color fill = ThemeColors.Mix(tint, panel, 0.14);
        context.DrawRectangle(new SolidColorBrush(fill), null, bounds, Radius, Radius);
        double strength = 0.55 + (0.45 * BeatPulse.Value);
        Color glow = ThemeColors.Fade(tint, 0.22 * strength);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(glow), 3), bounds.Inflate(1.5), Radius + 1.5, Radius + 1.5);
        Pen dashed = new(new SolidColorBrush(tint), 2) { DashStyle = new DashStyle([3, 2], 0) };
        context.DrawRectangle(null, dashed, bounds.Deflate(1), Radius, Radius);
    }

    private void Update()
    {
        PseudoClasses.Set(LiveClass, State == RegionState.Live);
        PseudoClasses.Set(QueuedClass, State == RegionState.Queued);
        TintBrush = new SolidColorBrush(Tint);
        Color ink = ThemeColors.Find(this, "EditorCueInk");
        Color text = ThemeColors.Find(this, "EditorTextBrush");
        if (State == RegionState.Live)
        {
            Ink = new SolidColorBrush(ink);
            KeyBoxBrush = new SolidColorBrush(ThemeColors.Fade(ink, 0.2));
        }
        else
        {
            Ink = new SolidColorBrush(text);
            KeyBoxBrush = new SolidColorBrush(Tint);
        }

        ShownIcon = State switch
        {
            RegionState.Live => LucideIcons.Repeat,
            RegionState.Queued => LucideIcons.Hourglass,
            _ => Icon,
        };
        SetPulsing(State == RegionState.Queued);
        InvalidateVisual();
    }

    private void SetPulsing(bool pulsing)
    {
        if (pulsing == _pulsing)
        {
            return;
        }

        _pulsing = pulsing;
        if (pulsing)
        {
            BeatPulse.Listen(InvalidateVisual);
        }
        else
        {
            BeatPulse.Ignore(InvalidateVisual);
        }
    }
}
