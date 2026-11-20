using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>An enum property, chosen from its values.</summary>
public sealed partial class EnumFieldViewModel : FieldViewModel
{
    public EnumFieldViewModel(ComponentProperty property)
        : base(property)
    {
        Array values = Enum.GetValuesAsUnderlyingType(property.Type);
        List<object> options = new(values.Length);
        foreach (object value in values)
        {
            object option = Enum.ToObject(property.Type, value);
            options.Add(option);
        }

        Options = options;
    }

    public IReadOnlyList<object> Options { get; }

    [ObservableProperty]
    public partial object? Value { get; set; }

    protected override void Show(object? value)
    {
        Value = value;
    }

    partial void OnValueChanged(object? value)
    {
        if (value is not null)
        {
            Write(value);
        }
    }
}
