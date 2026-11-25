namespace FlickerVoid.Editor.Assets;

/// <summary>A content file or folder: its path in the content folder with forward slashes, its name without extensions, and its type.</summary>
public sealed record AssetEntry(string Path, string Name, AssetType Type);
