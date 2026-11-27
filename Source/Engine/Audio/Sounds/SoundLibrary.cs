using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Decodes the content folder's sound files (<c>.wav</c>, <c>.ogg</c>) and keeps each clip until its file changes on disk.</summary>
public sealed class SoundLibrary : IDisposable
{
    private readonly IContentManager _content;
    private readonly Dictionary<string, SoundClip> _clips = new(ContentManager.PathComparer);

    public SoundLibrary(IContentManager content)
    {
        _content = content;
        _content.Changed += HandleChanged;
    }

    /// <summary>The clip of the file at the content path; throws when the file is missing or can't be decoded.</summary>
    public SoundClip Load(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (_clips.TryGetValue(fullPath, out SoundClip? clip))
        {
            return clip;
        }

        clip = Read(fullPath);
        _clips[fullPath] = clip;
        return clip;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
    }

    private static SoundClip Read(string fullPath)
    {
        if (fullPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            using FileStream stream = File.OpenRead(fullPath);
            return OggReader.Read(stream);
        }

        byte[] file = File.ReadAllBytes(fullPath);
        return WavReader.Read(file);
    }

    private void HandleChanged(string fullPath)
    {
        _clips.Remove(fullPath);
    }
}
