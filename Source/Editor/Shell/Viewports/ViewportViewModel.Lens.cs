using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    private readonly Dictionary<string, object?> _looks = [];

    /// <summary>The mood the viewport draws: the <see cref="GetLayeredMood"/>, without its canvas while <see cref="ShowCanvas"/> is off.</summary>
    public LightingMood GetDrawnMood()
    {
        LightingMood drawn = GetLayeredMood();
        if (!ShowCanvas)
        {
            drawn.Canvas = null;
        }

        return drawn;
    }

    /// <summary>
    /// The mood layered as the game does: the lens the scene's main camera names, then the values its <see cref="Mood"/>
    /// changes from the defaults; without the lens when it can't be read.
    /// </summary>
    public LightingMood GetLayeredMood()
    {
        LightingMood layered = new();
        string? path = MainCamera.FindLens(Scene.Game.World);
        if (Load(path, RenderingJsonContext.Default.CameraLens) is { } lens)
        {
            lens.Values.Apply(layered);
        }

        LayChangedValues(layered);
        return layered;
    }

    /// <summary>Writes onto a mood the values of <see cref="Mood"/> that differ from the defaults, the ones its file names.</summary>
    private void LayChangedValues(LightingMood drawn)
    {
        LightingMood defaults = new();
        foreach (MoodValue value in MoodValues.All)
        {
            LightingMood probe = defaults with { };
            value.Copy(Mood, probe);
            if (probe != defaults)
            {
                value.Copy(Mood, drawn);
            }
        }
    }

    private MoodOverride? LoadMood(string path)
    {
        return Load(path, RenderingJsonContext.Default.MoodOverride);
    }

    /// <summary>A file read once and kept until content changes; null for none named or one that can't be read.</summary>
    private T? Load<T>(string? path, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (_looks.TryGetValue(path, out object? kept))
        {
            return kept as T;
        }

        T? loaded = null;
        try
        {
            loaded = Scene.Content.Load(path, typeInfo);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or InvalidOperationException)
        {
            loaded = null;
        }

        _looks[path] = loaded;
        return loaded;
    }

    private void HandleContentChanged(string fullPath)
    {
        _looks.Clear();
    }
}
