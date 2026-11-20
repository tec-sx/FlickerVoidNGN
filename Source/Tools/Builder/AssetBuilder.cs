using FlickerVoid.Tools.Builder.Caching;
using FlickerVoid.Tools.Builder.Importing;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Tools.Builder;

/// <summary>
/// Builds a source content folder into an output folder: each file goes through the first importer that takes its extension,
/// or is copied, and files whose inputs haven't changed since the last build are skipped.
/// </summary>
public sealed class AssetBuilder(IReadOnlyList<IAssetImporter> importers)
{
    /// <summary>The schemas a development build writes into the source folder are for text editors, not the game.</summary>
    private const string SchemaFolder = ContentSchemas.FolderName + "/";

    private readonly CopyImporter _copy = new();

    public BuildReport Build(string sourceDirectory, string outputDirectory)
    {
        string source = Path.GetFullPath(sourceDirectory);
        string output = Path.GetFullPath(outputDirectory);
        string outputUnderSource = Path.GetRelativePath(source, output);
        bool outside = outputUnderSource.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(outputUnderSource);
        if (!outside)
        {
            throw new ArgumentException($"The output folder {output} can't be inside the source folder {source}.", nameof(outputDirectory));
        }

        BuildCache cache = BuildCache.Load(output);
        List<string> built = [];
        List<string> skipped = [];
        List<BuildFailure> failed = [];

        List<string> sourcePaths = FindSources(source);
        foreach (string path in sourcePaths)
        {
            IAssetImporter importer = FindImporter(path);
            cache.Entries.TryGetValue(path, out BuildCacheEntry? previous);
            if (IsUpToDate(previous, importer, source, output, path))
            {
                skipped.Add(path);
                continue;
            }

            BuildFailure? failure = BuildFile(importer, previous, source, output, path, cache);
            if (failure is null)
            {
                built.Add(path);
            }
            else
            {
                failed.Add(failure);
            }
        }

        List<string> removed = RemoveDeleted(cache, sourcePaths, output);
        cache.Save();
        return new BuildReport(built, skipped, failed, removed);
    }

    private static List<string> FindSources(string source)
    {
        List<string> paths = [];
        if (!Directory.Exists(source))
        {
            return paths;
        }

        foreach (string fullPath in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string path = AssetPaths.ToRelativePath(source, fullPath);
            if (path.StartsWith(SchemaFolder, StringComparison.Ordinal))
            {
                continue;
            }

            paths.Add(path);
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static bool IsUpToDate(BuildCacheEntry? previous, IAssetImporter importer, string source, string output, string path)
    {
        if (previous is null || previous.Importer != importer.Name)
        {
            return false;
        }

        foreach (string outputPath in previous.Outputs)
        {
            string fullPath = AssetPaths.ToFullPath(output, outputPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }
        }

        string hash = BuildCache.ComputeHash(importer, source, path, previous.Dependencies);
        return hash == previous.Hash;
    }

    /// <summary>Imports one file; a failure keeps its last outputs and leaves it to be built again next time.</summary>
    private static BuildFailure? BuildFile(IAssetImporter importer, BuildCacheEntry? previous, string source, string output, string path, BuildCache cache)
    {
        AssetImportContext context = new(source, output, path);
        try
        {
            importer.Import(context);
        }
        catch (Exception exception)
        {
            // Keeps what it wrote listed, so its outputs are still removed if the source is deleted.
            IEnumerable<string> previousOutputs = previous?.Outputs ?? [];
            List<string> written = [.. previousOutputs.Union(context.Outputs, StringComparer.Ordinal)];
            IReadOnlyList<string> previousDependencies = previous?.Dependencies ?? [];
            BuildCacheEntry retry = new(importer.Name, string.Empty, previousDependencies, written);
            cache.Set(path, retry);
            return new BuildFailure(path, exception.Message);
        }

        if (previous is not null)
        {
            IEnumerable<string> stale = previous.Outputs.Except(context.Outputs, StringComparer.Ordinal);
            DeleteOutputs(output, stale);
        }

        List<string> dependencies = [.. context.Dependencies.Order(StringComparer.Ordinal)];
        List<string> outputs = [.. context.Outputs.Order(StringComparer.Ordinal)];
        string hash = BuildCache.ComputeHash(importer, source, path, dependencies);
        BuildCacheEntry entry = new(importer.Name, hash, dependencies, outputs);
        cache.Set(path, entry);
        return null;
    }

    private static List<string> RemoveDeleted(BuildCache cache, List<string> sourcePaths, string output)
    {
        HashSet<string> present = new(sourcePaths, StringComparer.Ordinal);
        List<string> removed = [.. cache.Entries.Keys.Where(path => !present.Contains(path)).Order(StringComparer.Ordinal)];
        List<string> orphans = [];
        foreach (string path in removed)
        {
            BuildCacheEntry entry = cache.Entries[path];
            orphans.AddRange(entry.Outputs);
            cache.Remove(path);
        }

        // A renamed source may make the outputs its old name made, so only outputs no current source makes go.
        HashSet<string> current = [.. cache.Entries.Values.SelectMany(static entry => entry.Outputs)];
        IEnumerable<string> stale = orphans.Where(outputPath => !current.Contains(outputPath));
        DeleteOutputs(output, stale);
        return removed;
    }

    private static void DeleteOutputs(string output, IEnumerable<string> outputs)
    {
        foreach (string outputPath in outputs)
        {
            string fullPath = AssetPaths.ToFullPath(output, outputPath);
            File.Delete(fullPath);
        }
    }

    private IAssetImporter FindImporter(string path)
    {
        foreach (IAssetImporter importer in importers)
        {
            foreach (string extension in importer.Extensions)
            {
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return importer;
                }
            }
        }

        return _copy;
    }
}
