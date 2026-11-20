using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>Finds the content files with given extensions, by content path, for fields that name a file.</summary>
public sealed class ContentFileFinder(IContentManager content)
{
    /// <summary>The visible files ending in one of the extensions, ignoring case, sorted; the folders are read on every call.</summary>
    public IReadOnlyList<string> Find(IReadOnlyList<string> extensions)
    {
        List<string> found = [];
        List<AssetCatalog> catalogs = AssetCatalog.ForContent(content);
        foreach (AssetCatalog catalog in catalogs)
        {
            IReadOnlyList<AssetEntry> entries = catalog.Find(catalog.Prefix, recursive: true, kind: null, text: "");
            foreach (AssetEntry entry in entries)
            {
                if (extensions.Any(extension => entry.Path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    found.Add(entry.Path);
                }
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }
}
