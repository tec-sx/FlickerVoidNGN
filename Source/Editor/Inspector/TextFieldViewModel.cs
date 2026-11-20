using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>A string property, such as a file in the content folder; clearing an optional one sets it to null.</summary>
public sealed partial class TextFieldViewModel(ComponentProperty property) : FieldViewModel(property)
{
    [ObservableProperty]
    public partial string Value { get; set; } = "";

    protected override void Show(object? value)
    {
        Value = value as string ?? "";
    }

    partial void OnValueChanged(string value)
    {
        if (value.Length == 0 && Property.AcceptsNull)
        {
            Write(null);
            return;
        }

        Write(value);
    }
}
