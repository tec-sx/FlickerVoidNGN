using System.ComponentModel.DataAnnotations;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>A float or integer value, kept within the <see cref="RangeAttribute"/> that refines it, if any.</summary>
public partial class NumberFieldViewModel : FieldViewModel
{
    public NumberFieldViewModel(IEditableValue property)
        : base(property)
    {
        RangeAttribute? range = property.GetAttribute<RangeAttribute>();
        if (range is not null)
        {
            Minimum = Convert.ToDouble(range.Minimum, CultureInfo.InvariantCulture);
            Maximum = Convert.ToDouble(range.Maximum, CultureInfo.InvariantCulture);
        }
    }

    public double? Minimum { get; }

    public double? Maximum { get; }

    [ObservableProperty]
    public partial double Value { get; set; }

    protected override void Show(object? value)
    {
        if (value is float single)
        {
            Value = ToShown(single);
            return;
        }

        Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    partial void OnValueChanged(double value)
    {
        double limited = Limit(value);
        if (limited != value)
        {
            Value = limited;
            return;
        }

        object converted = Convert.ChangeType(value, Property.Type, CultureInfo.InvariantCulture);
        Write(converted);
    }

    private double Limit(double value)
    {
        double limited = value;
        if (Minimum is { } minimum && limited < minimum)
        {
            limited = minimum;
        }

        if (Maximum is { } maximum && limited > maximum)
        {
            limited = maximum;
        }

        return limited;
    }
}
