namespace FlickerVoid.Engine.Runtime.Data;

/// <summary>The rows of a data table file (<c>.fvtable</c>), keyed by name in the file's order, like an Unreal Data Table.</summary>
public sealed class DataTable<TRow>(IReadOnlyDictionary<string, TRow> rows)
    where TRow : class
{
    public IReadOnlyDictionary<string, TRow> Rows { get; } = rows;
}
