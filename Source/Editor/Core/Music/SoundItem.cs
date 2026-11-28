namespace FlickerVoid.Editor.Core.Music;

/// <summary>A file of Content the music editor lists to drag onto a lane: a sound file, a sound or a cue, with what is known of it.</summary>
public sealed class SoundItem(string path, Func<string, string> describe)
{
    private string? _detail;

    public string Path { get; } = path;

    public string Name { get; } = System.IO.Path.GetFileNameWithoutExtension(path);

    /// <summary>Its length in bars at the cue's tempo and, when it is not at it, the tempo found from its length; read the first time it is shown.</summary>
    public string Detail => _detail ??= describe(Path);
}
