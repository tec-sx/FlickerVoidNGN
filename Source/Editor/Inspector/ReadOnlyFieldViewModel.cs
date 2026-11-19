using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>A property the inspector has no editor for, shown as it would be written in a scene file.</summary>
public sealed partial class ReadOnlyFieldViewModel(IEditableValue property) : FieldViewModel(property)
{
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    protected override void Show(object? value)
    {
        Text = Property.ToJson(value);
    }
}
