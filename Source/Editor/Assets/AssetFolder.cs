namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>A folder of the content folder, by path with forward slashes ("" for the content folder itself), and its subfolders.</summary>
public sealed record AssetFolder(string Path, string Name, IReadOnlyList<AssetFolder> Children);
