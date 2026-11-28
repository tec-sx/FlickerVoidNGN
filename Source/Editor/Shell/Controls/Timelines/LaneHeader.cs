using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// The header of a timeline lane (rule R28): a 3 px edge in the lane's colour, its icon in that colour, its name, a note, a slot
/// for its buttons at the end (mute, solo) and its content under them (when it plays, gain, meter). A lane under another (fills)
/// is indented 22 px on the raised ground in the hint colour; one with no content centres its row; a selected one is washed in
/// the accent.
/// </summary>
[PseudoClasses(SubClass, CompactClass, SelectedClass)]
public class LaneHeader : ContentControl
{
    public const double Edge = 3;

    public const double SubIndent = 22;

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<LaneHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> NoteProperty = AvaloniaProperty.Register<LaneHeader, string?>(nameof(Note));

    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<LaneHeader, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<IBrush?> TintProperty = AvaloniaProperty.Register<LaneHeader, IBrush?>(nameof(Tint));

    public static readonly StyledProperty<object?> ButtonsProperty = AvaloniaProperty.Register<LaneHeader, object?>(nameof(Buttons));

    public static readonly StyledProperty<bool> IsSubProperty = AvaloniaProperty.Register<LaneHeader, bool>(nameof(IsSub));

    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<LaneHeader, bool>(nameof(IsSelected));

    private const string SubClass = ":sub";

    private const string CompactClass = ":compact";

    private const string SelectedClass = ":selected";

    public LaneHeader()
    {
        UpdateClasses();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>A few faint words after the name, or at the end of a lane under another.</summary>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The lane's colour, for the edge and the icon.</summary>
    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>The small buttons at the end of the name's row.</summary>
    public object? Buttons
    {
        get => GetValue(ButtonsProperty);
        set => SetValue(ButtonsProperty, value);
    }

    /// <summary>Whether the lane is under another, as a layer's fills.</summary>
    public bool IsSub
    {
        get => GetValue(IsSubProperty);
        set => SetValue(IsSubProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSubProperty || change.Property == ContentProperty || change.Property == IsSelectedProperty)
        {
            UpdateClasses();
        }
    }

    private void UpdateClasses()
    {
        PseudoClasses.Set(SubClass, IsSub);
        PseudoClasses.Set(CompactClass, IsSub || Content is null);
        PseudoClasses.Set(SelectedClass, IsSelected);
    }
}
