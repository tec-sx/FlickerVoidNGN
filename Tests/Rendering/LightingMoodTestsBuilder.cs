using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Writes mood files to a temporary content folder and loads them back.</summary>
internal sealed class LightingMoodTestsBuilder : IDisposable
{
    private readonly string _contentFolder = Path.Combine(Path.GetTempPath(), $"moods-{Guid.NewGuid():N}");

    public LightingMoodTestsBuilder()
    {
        Directory.CreateDirectory(_contentFolder);
        Content = new ContentManager(_contentFolder);
    }

    public ContentManager Content { get; }

    public LightingMoodTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public LightingMood Load(string path)
    {
        return Content.Load(path, RenderingJsonContext.Default.LightingMood);
    }

    public void Dispose()
    {
        Directory.Delete(_contentFolder, recursive: true);
    }
}
