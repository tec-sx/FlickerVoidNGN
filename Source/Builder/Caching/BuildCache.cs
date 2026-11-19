using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FlickerVoid.Engine.Builder.Importing;

namespace FlickerVoid.Engine.Builder.Caching;

/// <summary>Remembers how each source file was built, in a file in the output folder, so unchanged files are skipped.</summary>
public sealed class BuildCache
{
    /// <summary>The cache file's name in the output folder.</summary>
    public const string FileName = ".build-cache.json";

    private const string MissingFileMarker = "<missing>";

    private readonly string _filePath;
    private readonly Dictionary<string, BuildCacheEntry> _entries;

    private BuildCache(string filePath, Dictionary<string, BuildCacheEntry> entries)
    {
        _filePath = filePath;
        _entries = entries;
    }

    public IReadOnlyDictionary<string, BuildCacheEntry> Entries => _entries;

    /// <summary>Reads the cache in <paramref name="outputDirectory"/>; a missing or unreadable one starts empty, so everything builds.</summary>
    public static BuildCache Load(string outputDirectory)
    {
        string filePath = Path.Combine(outputDirectory, FileName);
        Dictionary<string, BuildCacheEntry> entries = new(StringComparer.Ordinal);
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                BuildCacheData? data = JsonSerializer.Deserialize(json, BuilderJsonContext.Default.BuildCacheData);
                if (data is not null)
                {
                    entries = new Dictionary<string, BuildCacheEntry>(data.Entries, StringComparer.Ordinal);
                }
            }
            catch (JsonException)
            {
                // A damaged cache only costs a full build.
            }
        }

        return new BuildCache(filePath, entries);
    }

    /// <summary>Hashes the importer's name and version, then the source and each dependency's path and content.</summary>
    public static string ComputeHash(IAssetImporter importer, string sourceDirectory, string sourcePath, IEnumerable<string> dependencies)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendText(hash, $"{importer.Name}@{importer.Version}");
        AppendFile(hash, sourceDirectory, sourcePath);
        foreach (string dependency in dependencies)
        {
            AppendFile(hash, sourceDirectory, dependency);
        }

        byte[] digest = hash.GetHashAndReset();
        return Convert.ToHexString(digest);
    }

    public void Set(string sourcePath, BuildCacheEntry entry)
    {
        _entries[sourcePath] = entry;
    }

    public void Remove(string sourcePath)
    {
        _entries.Remove(sourcePath);
    }

    public void Save()
    {
        string folder = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(folder);
        BuildCacheData data = new(_entries);
        string json = JsonSerializer.Serialize(data, BuilderJsonContext.Default.BuildCacheData);
        File.WriteAllText(_filePath, json);
    }

    private static void AppendFile(IncrementalHash hash, string sourceDirectory, string path)
    {
        string fullPath = AssetPaths.ToFullPath(sourceDirectory, path);
        if (!File.Exists(fullPath))
        {
            AppendText(hash, $"{path} {MissingFileMarker}");
            return;
        }

        byte[] content = File.ReadAllBytes(fullPath);
        AppendText(hash, $"{path} {content.Length}");
        hash.AppendData(content);
    }

    private static void AppendText(IncrementalHash hash, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text + "\n");
        hash.AppendData(bytes);
    }
}
