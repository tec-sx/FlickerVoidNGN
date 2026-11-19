using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>A true-or-false property, shown as a check box.</summary>
public sealed partial class BoolFieldViewModel(IEditableValue property) : FieldViewModel(property)
{
    [ObservableProperty]
    public partial bool Value { get; set; }

    protected override void Show(object? value)
    {
        Value = value is true;
    }

    partial void OnValueChanged(bool value)
    {
        Write(value);
    }
}
