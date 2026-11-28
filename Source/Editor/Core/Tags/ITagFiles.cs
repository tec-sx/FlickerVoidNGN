namespace FlickerVoid.Editor.Core.Tags;

/// <summary>Rewrites the project's files that hold a tag when it is renamed or removed; false, with why, when it can't now.</summary>
public interface ITagFiles
{
    bool Rename(string name, string to, out string? why);

    bool Remove(string name, out string? why);
}
