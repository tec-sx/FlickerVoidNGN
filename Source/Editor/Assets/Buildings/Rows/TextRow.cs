using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>A line of text, such as a name or a key's tag; each change is handed to the row's action.</summary>
public sealed partial class TextRow : ToolRow
{
    private Action<string> _set;
    private bool _taking;

    public TextRow(string label, string value, Action<string> set)
        : base(label)
    {
        _set = set;
        _taking = true;
        Text = value;
        _taking = false;
    }

    [ObservableProperty]
    public partial string Text { get; set; }

    public override void Take(ToolRow other)
    {
        TextRow row = (TextRow)other;
        _set = row._set;
        _taking = true;
        Text = row.Text;
        _taking = false;
    }

    partial void OnTextChanged(string value)
    {
        if (!_taking && !IsRetired)
        {
            _set(value);
        }
    }
}
