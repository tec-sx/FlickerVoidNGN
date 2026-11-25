using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

/// <summary>Writes mood files to a temporary content folder and loads them back as overrides.</summary>
internal sealed class MoodOverrideTestsBuilder : IDisposable
{
    private readonly string _contentFolder = Path.Combine(Path.GetTempPath(), $"mood-overrides-{Guid.NewGuid():N}");
    private readonly ContentManager _content;

    public MoodOverrideTestsBuilder()
    {
        Directory.CreateDirectory(_contentFolder);
        _content = new ContentManager(_contentFolder);
    }

    public MoodOverride Load(string json)
    {
        string path = $"{Guid.NewGuid():N}.fvmood";
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, json);
        return _content.Load(path, RenderingJsonContext.Default.MoodOverride);
    }

    public void Dispose()
    {
        Directory.Delete(_contentFolder, recursive: true);
    }
}
