using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    private readonly Dictionary<string, object?> _looks = [];

    /// <summary>
    /// The mood the viewport draws: its <see cref="Mood"/> with the lens the scene's main camera names laid over it (until the
    /// mood editor reads moods as the values they name), and without the lens when it can't be read.
    /// </summary>
    public LightingMood GetDrawnMood()
    {
        LightingMood drawn = Mood with { };
        string? path = MainCamera.FindLens(Scene.Game.World);
        if (Load(path, RenderingJsonContext.Default.CameraLens) is { } lens)
        {
            lens.Values.Apply(drawn);
        }

        return drawn;
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
