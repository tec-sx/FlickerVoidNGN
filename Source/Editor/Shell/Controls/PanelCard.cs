using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls;

/// <summary>
/// A card of a side panel, as the inspector shows a component: a 24 px header (a chevron, an icon, the title, then the card's
/// actions and its More menu) over a body of <see cref="PanelRow"/>s whose labels share one column. A click on the header folds
/// it; folded, the header sinks into the panel and says what the card holds. Every panel is built from these (rule R26).
/// </summary>
[PseudoClasses(ExpandedClass, MenuClass)]
public class PanelCard : HeaderedContentControl
{
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<PanelCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PanelCard, bool>(nameof(IsExpanded), true, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> NoteProperty = AvaloniaProperty.Register<PanelCard, string?>(nameof(Note));

    public static readonly StyledProperty<string?> SummaryProperty = AvaloniaProperty.Register<PanelCard, string?>(nameof(Summary));

    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<PanelCard, object?>(nameof(Actions));

    public static readonly StyledProperty<FlyoutBase?> MenuProperty = AvaloniaProperty.Register<PanelCard, FlyoutBase?>(nameof(Menu));

    public static readonly StyledProperty<bool> ShowsMenuProperty = AvaloniaProperty.Register<PanelCard, bool>(nameof(ShowsMenu), true);

    private const string ExpandedClass = ":expanded";

    private const string MenuClass = ":menu";

    public PanelCard()
    {
        UpdateClasses();
    }

    /// <summary>The icon before the title; none leaves no room for one.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>A few faint words after the title, open or folded: a count, or what the card belongs to.</summary>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>What the card holds, in one line, shown after the title only while it is folded (rule R12).</summary>
    public string? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>What the header's end holds, before the More button: the card's own small icon buttons.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>The menu of the More button at the header's end; none leaves the button out.</summary>
    public FlyoutBase? Menu
    {
        get => GetValue(MenuProperty);
        set => SetValue(MenuProperty, value);
    }

    /// <summary>Whether the More button shows while there is a <see cref="Menu"/>, for a menu only some cards of a list offer.</summary>
    public bool ShowsMenu
    {
        get => GetValue(ShowsMenuProperty);
        set => SetValue(ShowsMenuProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty || change.Property == MenuProperty || change.Property == ShowsMenuProperty)
        {
            UpdateClasses();
        }
        else if (change.Property == ActionsProperty)
        {
            AdoptActions(change.OldValue, change.NewValue);
        }
    }

    private void UpdateClasses()
    {
        PseudoClasses.Set(ExpandedClass, IsExpanded);
        bool menu = Menu is not null && ShowsMenu;
        PseudoClasses.Set(MenuClass, menu);
    }

    /// <summary>Keeps the actions among the card's logical children, as its header and content are, so bindings and styles reach them.</summary>
    private void AdoptActions(object? old, object? actions)
    {
        if (old is ILogical before)
        {
            LogicalChildren.Remove(before);
        }

        if (actions is ILogical after)
        {
            LogicalChildren.Add(after);
        }
    }
}
