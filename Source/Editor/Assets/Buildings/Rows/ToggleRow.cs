using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>A switch; each change is handed to the row's action.</summary>
public sealed partial class ToggleRow : ToolRow
{
    private Action<bool> _set;
    private bool _taking;

    public ToggleRow(string label, bool value, Action<bool> set)
        : base(label)
    {
        _set = set;
        _taking = true;
        IsOn = value;
        _taking = false;
    }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    public override void Take(ToolRow other)
    {
        ToggleRow row = (ToggleRow)other;
        _set = row._set;
        _taking = true;
        IsOn = row.IsOn;
        _taking = false;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (!_taking && !IsRetired)
        {
            _set(value);
        }
    }
}
