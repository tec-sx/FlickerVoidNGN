using System.Text.Json;

using FlickerVoid.Editor.Core.Environments;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Core.Tests.Environments;

/// <summary>
/// Builds the environment editor over an environment written as JSON, by default exposure rising in a straight line from
/// 0.2 at midnight to 1 at noon and a fog color, counting the edits it reports; its curve view is 240 by 100 pixels.
/// </summary>
internal sealed class EnvironmentEditorViewModelTestsBuilder
{
    public const string Street = """
        {
          "numbers": { "exposure": { "keys": [ { "time": 0, "value": 0.2, "mode": "Linear" }, { "time": 12, "value": 1, "mode": "Linear" } ] } },
          "colors": { "fogColor": { "keys": [ { "time": 6, "color": [0.5, 0.6, 0.7] } ] } }
        }
        """;

    private string _json = Street;

    public LightingEnvironment Environment { get; private set; } = new();

    public int Edits { get; private set; }

    public EnvironmentEditorViewModelTestsBuilder WithEnvironment(string json)
    {
        _json = json;
        return this;
    }

    public EnvironmentEditorViewModel Build()
    {
        Environment = JsonSerializer.Deserialize(_json, RenderingJsonContext.Default.LightingEnvironment)!;
        EnvironmentEditorViewModel editor = new("Street", Environment, () => Edits++);
        editor.Curves.Range.Resize(240, 100);
        return editor;
    }
}
