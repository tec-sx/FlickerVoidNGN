namespace FlickerVoid.Engine.Builder;

/// <summary>Converts between full paths and the forward-slash paths under a folder that the build cache and reports use.</summary>
public static class AssetPaths
{
    public static string ToFullPath(string directory, string path)
    {
        string fullPath = Path.GetFullPath(path, directory);
        ToRelativePath(directory, fullPath);
        return fullPath;
    }

    /// <summary>Returns <paramref name="fullPath"/> under <paramref name="directory"/>; a path outside it throws.</summary>
    public static string ToRelativePath(string directory, string fullPath)
    {
        string relativePath = Path.GetRelativePath(directory, fullPath);
        bool outside = relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (outside || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"{fullPath} is outside {directory}.");
        }

        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }
}
