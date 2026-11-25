namespace FlickerVoid.Editor.Assets.Data;

/// <summary>A data table's rows, in order and by name, and the type every row has.</summary>
public sealed record TableRows(Type RowType, List<KeyValuePair<string, object>> Rows);
