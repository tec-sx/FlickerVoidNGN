using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// The transport bar of every timeline (rule R28): play and pause on the accent, stop, the loop toggle, the position in 15 px and
/// the clock time faint, then the editor's fields after a separator and its tools at the end, on a 34 px bar.
/// </summary>
[TemplatePart(PlayPart, typeof(Button))]
[TemplatePart(StopPart, typeof(Button))]
public class Transport : TemplatedControl
{
    public const double BarHeight = 34;

    public static readonly StyledProperty<ITransport?> SourceProperty = AvaloniaProperty.Register<Transport, ITransport?>(nameof(Source));

    public static readonly StyledProperty<bool> ShowsStopProperty = AvaloniaProperty.Register<Transport, bool>(nameof(ShowsStop), true);

    public static readonly StyledProperty<bool> ShowsLoopProperty = AvaloniaProperty.Register<Transport, bool>(nameof(ShowsLoop), true);

    public static readonly StyledProperty<object?> FieldsProperty = AvaloniaProperty.Register<Transport, object?>(nameof(Fields));

    public static readonly StyledProperty<object?> ToolsProperty = AvaloniaProperty.Register<Transport, object?>(nameof(Tools));

    private const string PlayPart = "PART_Play";

    private const string StopPart = "PART_Stop";

    private Button? _play;
    private Button? _stop;

    public ITransport? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool ShowsStop
    {
        get => GetValue(ShowsStopProperty);
        set => SetValue(ShowsStopProperty, value);
    }

    public bool ShowsLoop
    {
        get => GetValue(ShowsLoopProperty);
        set => SetValue(ShowsLoopProperty, value);
    }

    /// <summary>The editor's fields after the position (tempo, meter, speed).</summary>
    public object? Fields
    {
        get => GetValue(FieldsProperty);
        set => SetValue(FieldsProperty, value);
    }

    /// <summary>The editor's tools at the bar's end (zoom, fit).</summary>
    public object? Tools
    {
        get => GetValue(ToolsProperty);
        set => SetValue(ToolsProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _play?.Click -= HandlePlay;
        _stop?.Click -= HandleStop;
        _play = e.NameScope.Find<Button>(PlayPart);
        _stop = e.NameScope.Find<Button>(StopPart);
        _play?.Click += HandlePlay;
        _stop?.Click += HandleStop;
    }

    private void HandlePlay(object? sender, RoutedEventArgs e) => Source?.TogglePlay();

    private void HandleStop(object? sender, RoutedEventArgs e) => Source?.Stop();
}
