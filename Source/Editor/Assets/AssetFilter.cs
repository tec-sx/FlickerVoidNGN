namespace FlickerVoid.Editor.Assets;

/// <summary>A choice in the asset browser's kind filter: one kind, or every kind when <see cref="Kind"/> is null.</summary>
public sealed record AssetFilter(string Label, AssetKind? Kind);
