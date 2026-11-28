using Avalonia.Controls;
using Avalonia.Controls.Primitives;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Tags;

namespace FlickerVoid.Editor.Tags;

/// <summary>A tag field's chips and its "+ Tag" chip, which opens a picker over the project's tags.</summary>
public partial class TagChipsView : UserControl
{
    public TagChipsView()
    {
        InitializeComponent();
    }

    /// <summary>Gives the picker a fresh tree each time it opens, as the project's tags may have changed.</summary>
    private void HandlePickerOpening(object? sender, EventArgs args)
    {
        if (sender is not Flyout { Content: TagPickerView picker } || DataContext is not TagsFieldViewModel field)
        {
            return;
        }

        TagPickerViewModel model = field.OpenPicker(TagCatalog.Current);
        picker.DataContext = model;
    }
}
