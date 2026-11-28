using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>An item of a list in a section: its icon's name, its name, a fainter value on the right, and what a click does.</summary>
public sealed partial class ListRow : ToolRow
{
    private Action? _open;

    public ListRow(string label, string value, string icon, Action? open = null, bool isWarning = false, bool isChosen = false)
        : base(label)
    {
        Value = value;
        Icon = icon;
        IsWarning = isWarning;
        IsChosen = isChosen;
        _open = open;
        OpenCommand = new RelayCommand(() => _open?.Invoke());
    }

    [ObservableProperty]
    public partial string Value { get; set; }

    public string Icon { get; }

    public bool IsWarning { get; }

    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    public IRelayCommand OpenCommand { get; }

    public override bool Matches(ToolRow other) => base.Matches(other) && ((ListRow)other).Icon == Icon && ((ListRow)other).IsWarning == IsWarning;

    public override void Take(ToolRow other)
    {
        ListRow row = (ListRow)other;
        _open = row._open;
        Value = row.Value;
        IsChosen = row.IsChosen;
    }
}
