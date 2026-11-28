using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>A number with its unit, dragged or typed; each change is handed to the row's action.</summary>
public sealed partial class NumberRow : ToolRow
{
    private Action<float> _set;
    private bool _taking;

    public NumberRow(string label, float value, string unit, Action<float> set, double minimum = double.MinValue, double maximum = double.MaxValue, double step = 0.05)
        : base(label)
    {
        _set = set;
        Unit = unit;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        _taking = true;
        Value = value;
        _taking = false;
    }

    public string Unit { get; }

    [ObservableProperty]
    public partial double Minimum { get; private set; }

    [ObservableProperty]
    public partial double Maximum { get; private set; }

    public double Step { get; }

    [ObservableProperty]
    public partial double Value { get; set; }

    public override void Take(ToolRow other)
    {
        NumberRow row = (NumberRow)other;
        _set = row._set;
        _taking = true;
        Minimum = row.Minimum;
        Maximum = row.Maximum;
        Value = row.Value;
        _taking = false;
    }

    partial void OnValueChanged(double value)
    {
        if (!_taking && !IsRetired)
        {
            _set((float)value);
        }
    }
}
