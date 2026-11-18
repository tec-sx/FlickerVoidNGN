using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>Loads content files by their path under a root folder and keeps each loaded object until it is unloaded.</summary>
public interface IContentManager
{
    string RootDirectory { get; }

    /// <summary>Returns the object loaded from the JSON file at <paramref name="path"/>, reading the file only the first time.</summary>
    T Load<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : class;

    /// <summary>Forgets the object loaded from <paramref name="path"/>, so the next load reads the file again.</summary>
    bool Unload(string path);

    /// <summary>Resolves a content path against the root; paths that lead outside the root throw.</summary>
    string GetFullPath(string path);
}
