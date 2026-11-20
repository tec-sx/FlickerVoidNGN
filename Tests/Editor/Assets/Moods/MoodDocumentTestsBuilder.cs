using FlickerVoid.Editor.Assets.Moods;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Tests.Moods;

/// <summary>Builds a mood document over Night.fvmood in a temporary content folder, with the editor's unsaved content.</summary>
internal sealed class MoodDocumentTestsBuilder : IDisposable
{
    public const string Night = "Night.fvmood";

    private readonly UnsavedContentTestsBuilder _files = new();

    public MoodDocumentTestsBuilder()
    {
        _files.WithFile(Night, """{ "$schema": "../.schemas/mood.schema.json", "exposure": 0.5 }""");
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public MoodDocument Build() => new(Content, Night);

    public LightingMood Load() => Content.Load(Night, RenderingJsonContext.Default.LightingMood);

    public string ReadFile() => _files.ReadFile(Night);

    public void Dispose() => _files.Dispose();
}
