namespace FlickerVoid.Editor.Core.Tags;

/// <summary>A row of the tag tree: the tag's full name, the last part of it, how deep it sits and whether it has children.</summary>
public readonly record struct TagRow(string Path, string Name, int Depth, bool HasChildren, bool IsFolded);
