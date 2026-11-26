using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Sky;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>
/// Builds the mood on screen from layers, lowest first: the defaults, the environment at the hour of the day with the sun and moon on their path, then the
/// overrides of the lens, the scene, the story, the shot and the tools, each changing only the values it names. When a layer changes,
/// the mood on screen blends to the new build over the change's seconds, still following the hour.
/// </summary>
public sealed class MoodDirector
{
    private static readonly int LayerCount = Enum.GetValues<MoodLayer>().Length;

    private readonly MoodOverride?[] _layers = new MoodOverride?[LayerCount];
    private LightingMood _from = new();
    private float _blendSeconds;
    private float _elapsed;
    private double _hour;
    private int _day;
    private bool _changed = true;

    /// <summary>The mood to draw with now; it stays the same object while nothing changes, and is never changed once handed out.</summary>
    public LightingMood Current { get; private set; } = new();

    /// <summary>True from a blended change until its seconds have passed.</summary>
    public bool IsBlending { get; private set; }

    public LightingEnvironment? Environment { get; private set; }

    /// <summary>The way the sun crosses the sky, which turns the sun's direction with the hour under the layers; null leaves it.</summary>
    public SunPath? SunPath { get; private set; }

    public MoodOverride? GetLayer(MoodLayer layer) => _layers[(int)layer];

    /// <summary>Changes the environment under the layers, or takes it away with null, blending over the seconds.</summary>
    public void SetEnvironment(LightingEnvironment? environment, float blendSeconds)
    {
        if (ReferenceEquals(environment, Environment))
        {
            return;
        }

        Environment = environment;
        Change(blendSeconds);
    }

    /// <summary>Changes the sun's path, or takes it away with null, at once.</summary>
    public void SetSunPath(SunPath? path)
    {
        if (path == SunPath)
        {
            return;
        }

        SunPath = path;
        Change(0);
    }

    /// <summary>Changes a layer's override, or takes it away with null, blending over the seconds; 0 shows it at once.</summary>
    public void SetLayer(MoodLayer layer, MoodOverride? mood, float blendSeconds)
    {
        int index = (int)layer;
        if (ReferenceEquals(mood, _layers[index]))
        {
            return;
        }

        _layers[index] = mood;
        Change(blendSeconds);
    }

    /// <summary>Builds the mood for an hour of the first day, a frame of the seconds given after the last.</summary>
    public void Update(double hour, float deltaSeconds) => Update(0, hour, deltaSeconds);

    /// <summary>Builds the mood for a day (which moves the moon) and an hour of it, a frame of the seconds given after the last.</summary>
    public void Update(int day, double hour, float deltaSeconds)
    {
        bool followsHour = Environment is not null || SunPath is not null;
        bool dayMoved = SunPath is not null && day != _day;
        bool hourMoved = followsHour && (hour != _hour || dayMoved);
        if (!_changed && !IsBlending && !hourMoved)
        {
            return;
        }

        _hour = hour;
        _day = day;
        _changed = false;
        LightingMood target = Build(day, hour);
        if (IsBlending)
        {
            _elapsed += deltaSeconds;
            IsBlending = _elapsed < _blendSeconds;
        }

        if (IsBlending)
        {
            Current = LightingMood.Lerp(_from, target, _elapsed / _blendSeconds);
        }
        else
        {
            Current = target;
        }
    }

    /// <summary>
    /// Starts a blend from the mood on screen. Of changes made between two updates the quickest decides, so a cut to another
    /// scene is not blended by a story mood that changed with it, and nothing blends before the first update.
    /// </summary>
    private void Change(float blendSeconds)
    {
        if (_changed)
        {
            _blendSeconds = MathF.Min(_blendSeconds, blendSeconds);
        }
        else
        {
            _from = Current;
            _blendSeconds = blendSeconds;
        }

        _changed = true;
        _elapsed = 0;
        IsBlending = _blendSeconds > 0;
    }

    private LightingMood Build(int day, double hour)
    {
        LightingMood mood = new();
        Environment?.Apply((float)hour, mood);
        SunPath?.Apply(day, hour, mood);
        foreach (MoodOverride? layer in _layers)
        {
            layer?.Apply(mood);
        }

        return mood;
    }
}
