namespace FlickerVoid.Editor.Assets;

/// <summary>A choice in the asset browser's type filter: one type, or every type when <see cref="Type"/> is null.</summary>
public sealed record AssetFilter(string Label, AssetType? Type);
