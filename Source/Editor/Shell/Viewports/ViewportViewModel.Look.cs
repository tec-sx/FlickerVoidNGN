using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    private static readonly QueryDescription Environments = new QueryDescription().WithAll<SceneEnvironment>();

    private readonly Dictionary<string, object?> _looks = [];

    /// <summary>The hour of the day the viewport draws the scene's environment at; a scene opens at its start hour.</summary>
    [ObservableProperty]
    public partial float Hour { get; set; } = 12;

    /// <summary>The environment file an editor has open, drawn in place of the scene's own; null draws the scene's.</summary>
    public string? EnvironmentPath { get; set; }

    /// <summary>The mood file the View tab edits, whose values the viewport lays over the environment and lens; null for none.</summary>
    public string? MoodPath { get; set; }

    /// <summary>
    /// The mood the viewport draws, built as the game builds it: under it the View tab's <see cref="Mood"/> when no mood file is
    /// open, else the defaults; then the environment an editor has open or else the scene's, at <see cref="Hour"/>, the lens the main camera names, and the
    /// values the mood file names. A file that can't be read is left out.
    /// </summary>
    public LightingMood GetDrawnMood()
    {
        World world = Scene.Game.World;
        MoodOverride? file = LoadMood(MoodPath);
        LightingMood drawn = file is null ? Mood with { } : new LightingMood();
        string? environmentPath = EnvironmentPath ?? SceneEnvironment.Find(world);
        if (Load(environmentPath, RenderingJsonContext.Default.LightingEnvironment) is { } environment)
        {
            environment.Apply(Hour, drawn);
        }

        string? lensPath = MainCamera.FindLens(world);
        if (Load(lensPath, RenderingJsonContext.Default.CameraLens) is { } lens)
        {
            lens.Values.Apply(drawn);
        }

        file?.Apply(drawn);
        return drawn;
    }

    private MoodOverride? LoadMood(string? path)
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

    /// <summary>Starts a scene with an environment at its start hour; one without keeps the hour shown.</summary>
    private void HandleSceneOpened()
    {
        _looks.Clear();
        World world = Scene.Game.World;
        float? start = null;
        world.Query(in Environments, (ref SceneEnvironment environment) =>
        {
            start ??= environment.StartHour;
        });
        if (start is float hour)
        {
            Hour = hour;
        }
    }
}
