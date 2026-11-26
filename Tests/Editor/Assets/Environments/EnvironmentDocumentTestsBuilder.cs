using FlickerVoid.Editor.Assets.Environments;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Tests.Environments;

/// <summary>Builds an environment document over Street.fvenv in a temporary content folder, with the editor's unsaved content.</summary>
internal sealed class EnvironmentDocumentTestsBuilder : IDisposable
{
    public const string Street = "Street.fvenv";

    private readonly UnsavedContentTestsBuilder _files = new();

    public EnvironmentDocumentTestsBuilder()
    {
        _files.WithFile(Street, """
            {
              "$schema": ".schemas/environment.schema.json",
              "numbers": { "exposure": { "keys": [ { "time": 6, "value": 0.4 } ] } }
            }
            """);
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public EnvironmentDocument Build() => new(Content, Street);

    public LightingEnvironment Load() => Content.Load(Street, RenderingJsonContext.Default.LightingEnvironment);

    public string ReadFile() => _files.ReadFile(Street);

    public void Dispose() => _files.Dispose();
}
