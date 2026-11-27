using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace FlickerVoid.Editor.Controls;

/// <summary>
/// A row of a <see cref="PanelCard"/>: a label beside its value, in a column 96 px wide or as wide as the card's longest label, so
/// the values line up. As the panel narrows the labels give way before the values do: a label is cut once its value would get
/// less than <see cref="ValueWidth"/>, and reads whole in its tooltip. A value that holds rows of its own goes under its label,
/// indented.
/// </summary>
[PseudoClasses(NestedClass)]
[TemplatePart(LabelPart, typeof(TextBlock))]
public class PanelRow : ContentControl
{
    /// <summary>The label column's width in a card of short labels, and the least it ever is.</summary>
    public const double LabelColumn = 96;

    /// <summary>What a value keeps as the panel narrows: its width beside the longest label at the side panel's default width.</summary>
    public const double ValueWidth = 124;

    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<PanelRow, string?>(nameof(Label));

    public static readonly StyledProperty<bool> IsNestedProperty = AvaloniaProperty.Register<PanelRow, bool>(nameof(IsNested));

    private const string NestedClass = ":nested";

    private const string LabelPart = "PART_Label";

    private const double LongestLabel = 170;

    private TextBlock? _label;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Whether the value holds rows of its own (an object, a list) and so goes under the label, across the row.</summary>
    public bool IsNested
    {
        get => GetValue(IsNestedProperty);
        set => SetValue(IsNestedProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<TextBlock>(LabelPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsNestedProperty)
        {
            PseudoClasses.Set(NestedClass, IsNested);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_label is not null)
        {
            double gap = _label.Margin.Left + _label.Margin.Right;
            double room = availableSize.Width - ValueWidth - gap;
            _label.MaxWidth = Math.Clamp(room, LabelColumn - gap, LongestLabel);
        }

        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size size = base.ArrangeOverride(finalSize);
        if (_label is not null)
        {
            bool cut = _label.TextLayout.TextLines.Any(line => line.HasCollapsed);
            string? whole = cut ? Label : null;
            ToolTip.SetTip(_label, whole);
        }

        return size;
    }
}
