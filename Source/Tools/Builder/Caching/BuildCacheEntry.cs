namespace FlickerVoid.Tools.Builder.Caching;

/// <summary>How a source file was last built: by which importer, the hash of everything it read, and what it wrote.</summary>
public sealed record BuildCacheEntry(string Importer, string Hash, IReadOnlyList<string> Dependencies, IReadOnlyList<string> Outputs);
