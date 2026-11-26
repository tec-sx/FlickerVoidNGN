namespace FlickerVoid.Editor.Assets;

/// <summary>
/// A chip of the content drawer's filter: a category of file types (Scenes, Art, Light, Data, Code) with its colour as
/// <c>#RRGGBB</c>, or every type when <see cref="Types"/> is empty.
/// </summary>
public sealed record AssetFilter(string Label, IReadOnlyList<AssetType> Types, string? Color = null)
{
    public bool IsAll => Types.Count == 0;

    public bool Matches(AssetType type) => IsAll || Types.Contains(type);
}
