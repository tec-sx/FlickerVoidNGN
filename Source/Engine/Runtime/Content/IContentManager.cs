using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>Loads content files by their path under a root folder and keeps each loaded object until it is unloaded.</summary>
public interface IContentManager
{
    /// <summary>Raised with a file's full path after it changed on disk and its loaded object was forgotten.</summary>
    event Action<string>? Changed;

    string RootDirectory { get; }

    /// <summary>Returns the object loaded from the JSON file at <paramref name="path"/>, reading the file only the first time.</summary>
    T Load<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : class;

    /// <summary>Forgets the object loaded from <paramref name="path"/>, so the next load reads the file again.</summary>
    bool Unload(string path);

    /// <summary>Forgets the object loaded from the file at <paramref name="path"/>, which changed on disk, and raises <see cref="Changed"/>.</summary>
    void NotifyChanged(string path);

    /// <summary>
    /// Saves a file at <paramref name="path"/> and reports it changed. In a development build (one with a
    /// <see cref="ContentBuildManifest"/>) its source file gets the same bytes, so the change outlives the next build.
    /// </summary>
    void Write(string path, byte[] contents);

    /// <summary>Creates a folder, and its source folder in a development build.</summary>
    void CreateFolder(string path);

    /// <summary>Deletes a file or a folder, with its source in a development build, and reports each deleted file changed.</summary>
    void Delete(string path);

    /// <summary>Moves or renames a file or a folder, with its source in a development build, and reports the old and new files changed.</summary>
    void Move(string path, string newPath);

    /// <summary>Resolves a content path against the root; paths that lead outside the root throw.</summary>
    string GetFullPath(string path);

    /// <summary>
    /// The full path of the source file the content at <paramref name="path"/> is built from, in a development build whose
    /// <see cref="ContentBuildManifest"/> names a source folder that holds it; its own full path otherwise.
    /// </summary>
    string GetSourcePath(string path);
}
