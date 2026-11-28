using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Mix;

/// <summary>
/// One channel of a mixer (rule R28): an 88 px strip with its name on a 22 px header under a 3 px bar of its colour, the fader,
/// two meters and the scale, the route it goes to, its gain and mute and solo. A bus strip has its name bold on the base colour;
/// a silent one fades to 55%. The music mixer's layers and buses are these; so will be any later mixer.
/// </summary>
[PseudoClasses(BusClass, SilentClass)]
public class ChannelStrip : TemplatedControl
{
    public const double StripWidth = 88;

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<ChannelStrip, string?>(nameof(Title));

    public static readonly StyledProperty<IBrush?> TintProperty = AvaloniaProperty.Register<ChannelStrip, IBrush?>(nameof(Tint));

    public static readonly StyledProperty<double> DecibelsProperty =
        AvaloniaProperty.Register<ChannelStrip, double>(nameof(Decibels), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<ChannelStrip, double>(nameof(Maximum), 6);

    public static readonly StyledProperty<double> LevelProperty = AvaloniaProperty.Register<ChannelStrip, double>(nameof(Level));

    public static readonly StyledProperty<string?> GainTextProperty = AvaloniaProperty.Register<ChannelStrip, string?>(nameof(GainText));

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<ChannelStrip, bool>(nameof(IsMuted), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSoloedProperty =
        AvaloniaProperty.Register<ChannelStrip, bool>(nameof(IsSoloed), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowsSoloProperty = AvaloniaProperty.Register<ChannelStrip, bool>(nameof(ShowsSolo), true);

    public static readonly StyledProperty<object?> RouteProperty = AvaloniaProperty.Register<ChannelStrip, object?>(nameof(Route));

    public static readonly StyledProperty<bool> IsBusProperty = AvaloniaProperty.Register<ChannelStrip, bool>(nameof(IsBus));

    private const string BusClass = ":bus";

    private const string SilentClass = ":silent";

    /// <summary>The level under which a strip reads as silent.</summary>
    private const double Silence = 0.0001;

    public ChannelStrip()
    {
        UpdateClasses();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public double Decibels
    {
        get => GetValue(DecibelsProperty);
        set => SetValue(DecibelsProperty, value);
    }

    /// <summary>The highest gain the fader sets; 0 for a bus.</summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public string? GainText
    {
        get => GetValue(GainTextProperty);
        set => SetValue(GainTextProperty, value);
    }

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public bool IsSoloed
    {
        get => GetValue(IsSoloedProperty);
        set => SetValue(IsSoloedProperty, value);
    }

    public bool ShowsSolo
    {
        get => GetValue(ShowsSoloProperty);
        set => SetValue(ShowsSoloProperty, value);
    }

    /// <summary>What picks the bus the channel goes to, under the fader; none leaves the room empty.</summary>
    public object? Route
    {
        get => GetValue(RouteProperty);
        set => SetValue(RouteProperty, value);
    }

    public bool IsBus
    {
        get => GetValue(IsBusProperty);
        set => SetValue(IsBusProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusProperty || change.Property == LevelProperty)
        {
            UpdateClasses();
        }
    }

    private void UpdateClasses()
    {
        PseudoClasses.Set(BusClass, IsBus);
        PseudoClasses.Set(SilentClass, Level < Silence);
    }
}
