namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>
/// Names the builder and the source folders a development build's content came from, so the running game can keep them built.
/// The game's build writes it into the content folder as <see cref="FileName"/>, one <c>key=value</c> per line
/// (<c>dotnet</c>, <c>builder</c>, and <c>folder=source|output folder under the content root</c>); published games leave it out.
/// </summary>
public sealed class ContentBuildManifest
{
    public const string FileName = ".content-build";

    private ContentBuildManifest(string dotnet, string builder, IReadOnlyList<ContentBuildFolder> folders)
    {
        Dotnet = dotnet;
        Builder = builder;
        Folders = folders;
    }

    public string Dotnet { get; }

    public string Builder { get; }

    public IReadOnlyList<ContentBuildFolder> Folders { get; }

    /// <summary>Reads the manifest in a content folder, or returns null when it has none.</summary>
    public static ContentBuildManifest? Find(string contentRoot)
    {
        string path = Path.Combine(contentRoot, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return Read(path);
    }

    public static ContentBuildManifest Read(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetDirectoryName(fullPath)!;
        string dotnet = "dotnet";
        string builder = string.Empty;
        List<ContentBuildFolder> folders = [];
        foreach (string line in File.ReadLines(path))
        {
            int separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            string key = line[..separator];
            string value = line[(separator + 1)..];
            switch (key)
            {
                case "dotnet":
                    dotnet = value;
                    break;
                case "builder":
                    builder = value;
                    break;
                case "folder":
                    string[] parts = value.Split('|');
                    string output = Path.GetFullPath(parts[1], root);
                    string trimmed = Path.TrimEndingDirectorySeparator(output);
                    folders.Add(new ContentBuildFolder(parts[0], trimmed));
                    break;
            }
        }

        return new ContentBuildManifest(dotnet, builder, folders);
    }
}
