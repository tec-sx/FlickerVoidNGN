using System.Text;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// Where imported content files came from, so they can be imported again after they change there: a hidden
/// <c>.imports</c> file at the content root, one <c>content path|full source path</c> per line.
/// </summary>
public sealed class ImportSources(IContentManager content)
{
    public const string FileName = ".imports";

    private Dictionary<string, string>? _sources;

    /// <summary>The full path the file was imported from, or null when it wasn't imported.</summary>
    public string? Find(string path)
    {
        Dictionary<string, string> sources = Read();
        return sources.GetValueOrDefault(path);
    }

    public void Set(string path, string source)
    {
        Dictionary<string, string> sources = Read();
        sources[path] = source;
        Write(sources);
    }

    /// <summary>Follows a file or folder to its new path.</summary>
    public void Move(string path, string newPath)
    {
        Dictionary<string, string> sources = Read();
        List<string> moved = FindUnder(sources, path);
        foreach (string old in moved)
        {
            string source = sources[old];
            sources.Remove(old);
            string renamed = newPath + old[path.Length..];
            sources[renamed] = source;
        }

        if (moved.Count > 0)
        {
            Write(sources);
        }
    }

    /// <summary>Forgets a deleted file, or everything in a deleted folder.</summary>
    public void Remove(string path)
    {
        Dictionary<string, string> sources = Read();
        List<string> removed = FindUnder(sources, path);
        foreach (string old in removed)
        {
            sources.Remove(old);
        }

        if (removed.Count > 0)
        {
            Write(sources);
        }
    }

    private static List<string> FindUnder(Dictionary<string, string> sources, string path)
    {
        string folder = path + "/";
        List<string> found = [];
        foreach (string key in sources.Keys)
        {
            if (key == path || key.StartsWith(folder, StringComparison.Ordinal))
            {
                found.Add(key);
            }
        }

        return found;
    }

    private Dictionary<string, string> Read()
    {
        if (_sources is not null)
        {
            return _sources;
        }

        _sources = new Dictionary<string, string>(StringComparer.Ordinal);
        string fullPath = content.GetFullPath(FileName);
        if (!File.Exists(fullPath))
        {
            return _sources;
        }

        foreach (string line in File.ReadAllLines(fullPath))
        {
            string[] parts = line.Split('|', 2);
            if (parts.Length == 2)
            {
                _sources[parts[0]] = parts[1];
            }
        }

        return _sources;
    }

    private void Write(Dictionary<string, string> sources)
    {
        StringBuilder text = new();
        foreach ((string path, string source) in sources.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            text.Append(path).Append('|').Append(source).Append('\n');
        }

        string contents = text.ToString();
        byte[] bytes = Encoding.UTF8.GetBytes(contents);
        content.Write(FileName, bytes);
    }
}
