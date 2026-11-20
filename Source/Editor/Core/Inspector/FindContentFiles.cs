namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>The content paths of the files ending in one of the extensions, for fields that name a content file.</summary>
public delegate IReadOnlyList<string> FindContentFiles(IReadOnlyList<string> extensions);
