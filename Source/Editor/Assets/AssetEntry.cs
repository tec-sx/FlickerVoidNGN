namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>A content file: its path in the content folder with forward slashes, its name without extensions, and its kind.</summary>
public sealed record AssetEntry(string Path, string Name, AssetKind Kind);
