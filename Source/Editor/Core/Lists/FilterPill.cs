namespace FlickerVoid.Editor.Core.Lists;

/// <summary>A pill of a filter row: its name, how many rows it shows, what it filters by (null for all), or the pill that adds one.</summary>
public sealed record FilterPill(string Name, int Count, object? Key = null, bool IsAdd = false)
{
    /// <summary>The count as the pill shows it, or empty for the add pill.</summary>
    public string CountText => IsAdd ? "" : Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
