namespace FlickerVoid.Engine.Builder.Caching;

/// <summary>The build cache file: an entry for each source file by its path under the source folder.</summary>
public sealed record BuildCacheData(Dictionary<string, BuildCacheEntry> Entries);
