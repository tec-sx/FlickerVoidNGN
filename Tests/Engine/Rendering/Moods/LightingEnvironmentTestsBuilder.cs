using System.Numerics;

using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Curves;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

/// <summary>
/// Builds environments from the keys added, or writes environment files to a temporary content folder to load them back
/// or check them as a game with rendering does.
/// </summary>
internal sealed class LightingEnvironmentTestsBuilder : IDisposable
{
    private readonly string _contentFolder = Path.Combine(Path.GetTempPath(), $"environments-{Guid.NewGuid():N}");
    private readonly Dictionary<string, List<CurveKey>> _numbers = [];
    private readonly Dictionary<string, List<ColorKey>> _colors = [];

    public LightingEnvironmentTestsBuilder()
    {
        Directory.CreateDirectory(_contentFolder);
    }

    public LightingEnvironmentTestsBuilder WithNumber(string name, float hour, float value)
    {
        if (!_numbers.TryGetValue(name, out List<CurveKey>? keys))
        {
            keys = [];
            _numbers.Add(name, keys);
        }

        CurveKey key = new() { Time = hour, Value = value };
        keys.Add(key);
        return this;
    }

    public LightingEnvironmentTestsBuilder WithColor(string name, float hour, float red, float green, float blue)
    {
        if (!_colors.TryGetValue(name, out List<ColorKey>? keys))
        {
            keys = [];
            _colors.Add(name, keys);
        }

        Vector3 color = new(red, green, blue);
        ColorKey key = new() { Time = hour, Color = color };
        keys.Add(key);
        return this;
    }

    public LightingEnvironmentTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public LightingEnvironment Build()
    {
        LightingEnvironment environment = new();
        foreach ((string name, List<CurveKey> keys) in _numbers)
        {
            environment.Numbers[name] = new Curve(keys);
        }

        foreach ((string name, List<ColorKey> keys) in _colors)
        {
            environment.Colors[name] = new ColorCurve(keys);
        }

        return environment;
    }

    public LightingEnvironment Load(string path)
    {
        ContentManager content = new(_contentFolder);
        return content.Load(path, RenderingJsonContext.Default.LightingEnvironment);
    }

    /// <summary>What the content check of a game with rendering finds wrong in the files written.</summary>
    public IReadOnlyList<ContentProblem> Check()
    {
        using Game game = new(NullLoggerFactory.Instance);
        game.AddRendering();
        return ContentCheck.Run(game, _contentFolder);
    }

    public void Dispose()
    {
        Directory.Delete(_contentFolder, recursive: true);
    }
}
