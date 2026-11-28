using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>One row of a building tool's section: a label, and what it shows or edits; a row shown again takes the new row's value.</summary>
public abstract partial class ToolRow(string label) : ObservableObject
{
    public string Label { get; } = label;

    /// <summary>Whether the row is the same kind of row with the same label, so it can take the other's value in place.</summary>
    public virtual bool Matches(ToolRow other) => other.GetType() == GetType() && other.Label == Label;

    /// <summary>Whether the row has left the tab, so a change still written to it (a box giving up the keyboard) edits nothing.</summary>
    public bool IsRetired { get; private set; }

    /// <summary>Takes the other row's value and action without running either.</summary>
    public abstract void Take(ToolRow other);

    public void Retire() => IsRetired = true;
}
