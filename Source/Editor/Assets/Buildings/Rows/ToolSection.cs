using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>A folding section of a building tool's tab: its title, its icon's name and its rows.</summary>
public sealed partial class ToolSection(string title, string icon, IReadOnlyList<ToolRow> rows, bool isExpanded = true) : ObservableObject
{
    public string Title { get; } = title;

    public string Icon { get; } = icon;

    public IReadOnlyList<ToolRow> Rows { get; } = rows;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = isExpanded;

    /// <summary>Whether the other section has the same title and rows of the same kinds and labels, so its values can be taken in place.</summary>
    public bool Matches(ToolSection other)
    {
        if (other.Title != Title || other.Rows.Count != Rows.Count)
        {
            return false;
        }

        for (int index = 0; index < Rows.Count; index++)
        {
            if (!Rows[index].Matches(other.Rows[index]))
            {
                return false;
            }
        }

        return true;
    }

    public void Take(ToolSection other)
    {
        for (int index = 0; index < Rows.Count; index++)
        {
            Rows[index].Take(other.Rows[index]);
        }
    }

    /// <summary>Retires the section's rows as it leaves the tab.</summary>
    public void Retire()
    {
        foreach (ToolRow row in Rows)
        {
            row.Retire();
        }
    }
}
