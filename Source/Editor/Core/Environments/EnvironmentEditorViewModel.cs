using System.Collections.ObjectModel;
using System.Numerics;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Curves;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Environments;

/// <summary>
/// An environment file's curves edited over its day: every value an environment can draw, in its group and shown at the
/// cursor's <see cref="Hour"/>, where editing it writes a key (at the quarter hour, replacing one already there); the selected
/// values' curves in <see cref="Curves"/> and colors in <see cref="ColorTracks"/>; and a clock that plays the hour on. The
/// environment is edited in place and <paramref name="edited"/> follows each change.
/// </summary>
public sealed partial class EnvironmentEditorViewModel : ObservableObject
{
    private const float Day = LightingEnvironment.DayHours;

    /// <summary>The step keys written from the fields snap to: a quarter of an hour.</summary>
    private const float KeyStep = 0.25f;

    /// <summary>Keys nearer each other than this, in hours, are at the same time.</summary>
    private const float SameTime = 1e-3f;

    private static readonly LightingMood Defaults = new();

    private readonly LightingEnvironment _environment;
    private readonly Action _edited;
    private readonly LightingMood _atHour = new();
    private readonly Dictionary<string, EnvironmentValueViewModel> _values = [];
    private bool _refreshing;

    public EnvironmentEditorViewModel(string title, LightingEnvironment environment, Action edited, FindContentFiles? files = null)
    {
        Title = title;
        _environment = environment;
        _edited = edited;
        environment.Apply(Hour, _atHour);
        List<EnvironmentGroupViewModel> groups = [];
        IEnumerable<IGrouping<MoodGroup, MoodValue>> grouped = MoodValues.All.Where(IsDrawable).GroupBy(value => value.Group);
        foreach (IGrouping<MoodGroup, MoodValue> group in grouped.OrderBy(group => group.Key))
        {
            List<EnvironmentValueViewModel> values = [];
            foreach (MoodValue value in group)
            {
                EnvironmentValueViewModel created = CreateValue(value, files);
                values.Add(created);
                _values.Add(value.Name, created);
            }

            groups.Add(new EnvironmentGroupViewModel(group.Key.ToString(), values));
        }

        Groups = groups;
    }

    public string Title { get; }

    public IReadOnlyList<EnvironmentGroupViewModel> Groups { get; }

    /// <summary>The selected numbers' curves, overlaid; the last one selected takes the pointer.</summary>
    public CurveEditorViewModel Curves { get; } = new();

    /// <summary>The selected colors' curves, each on its gradient strip.</summary>
    public ObservableCollection<ColorTrack> ColorTracks { get; } = [];

    /// <summary>The hour of the day the cursor is at, from 0 up to 24.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HourText))]
    public partial float Hour { get; set; } = 12;

    /// <summary>The cursor's hour as a clock shows it.</summary>
    public string HourText
    {
        get
        {
            int minutes = (int)MathF.Round(Hour * 60);
            return $"{minutes / 60 % 24:00}:{minutes % 60:00}";
        }
    }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    /// <summary>The hours of the day that pass in a real second while playing.</summary>
    [ObservableProperty]
    public partial float Speed { get; set; } = 1;

    public EnvironmentValueViewModel Find(string name) => _values[name];

    /// <summary>Moves the hour on by the real seconds passed, around the day, while playing.</summary>
    public void Advance(double seconds)
    {
        if (!IsPlaying)
        {
            return;
        }

        float hour = Hour + (float)(seconds * Speed);
        Hour = ((hour % Day) + Day) % Day;
    }

    [RelayCommand]
    public void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Takes a value's curve out of the environment, so what lies under it shows again.</summary>
    public void RemoveCurve(EnvironmentValueViewModel value)
    {
        value.IsSelected = false;
        bool removed = _environment.Numbers.Remove(value.Name) | _environment.Colors.Remove(value.Name);
        if (!removed)
        {
            return;
        }

        Refresh();
        _edited();
    }

    partial void OnHourChanged(float value) => Refresh();

    private static bool IsDrawable(MoodValue value)
    {
        return value.Owner == MoodOwner.Environment && value is MoodNumber or MoodColor;
    }

    private EnvironmentValueViewModel CreateValue(MoodValue value, FindContentFiles? files)
    {
        JsonPropertyInfo info = RenderingJsonContext.Default.LightingMood.Properties.First(property => property.Name == value.Name);
        EnvironmentValueViewModel? created = null;
        ObjectProperty property = new(_atHour, info, () => HandleFieldEdited(created!));
        FieldViewModel field = FieldFactory.Create(property, files);
        created = new EnvironmentValueViewModel(value, field, HandleSelected)
        {
            IsDrawn = IsDrawn(value.Name),
        };
        field.Refresh();
        return created;
    }

    private bool IsDrawn(string name) => _environment.Numbers.ContainsKey(name) || _environment.Colors.ContainsKey(name);

    private void HandleFieldEdited(EnvironmentValueViewModel value)
    {
        if (_refreshing)
        {
            return;
        }

        float time = MathF.Round(Hour / KeyStep) * KeyStep;
        if (time >= Day)
        {
            time -= Day;
        }

        if (value.Value is MoodNumber number)
        {
            float written = number.Get(_atHour);
            Curve curve = WriteKey(number.Name, time, written);
            FindTrack(number.Name)?.Reset(curve);
        }
        else if (value.Value is MoodColor color)
        {
            Vector3 written = color.Get(_atHour);
            ColorCurve curve = WriteKey(color.Name, time, written);
            ColorTracks.FirstOrDefault(track => track.Name == color.Name)?.Reset(curve);
        }

        Refresh();
        _edited();
    }

    private Curve WriteKey(string name, float time, float value)
    {
        IReadOnlyList<CurveKey> keys = _environment.Numbers.TryGetValue(name, out Curve? existing) ? existing.Keys : [];
        CurveKey key = new() { Time = time, Value = value };
        List<CurveKey> written = [];
        foreach (CurveKey kept in keys)
        {
            if (MathF.Abs(kept.Time - time) < SameTime)
            {
                key.Mode = kept.Mode;
                key.In = kept.In;
                key.Out = kept.Out;
            }
            else
            {
                written.Add(kept);
            }
        }

        written.Add(key);
        Curve curve = new(written);
        _environment.Numbers[name] = curve;
        return curve;
    }

    private ColorCurve WriteKey(string name, float time, Vector3 value)
    {
        IReadOnlyList<ColorKey> keys = _environment.Colors.TryGetValue(name, out ColorCurve? existing) ? existing.Keys : [];
        ColorKey key = new() { Time = time, Color = value };
        List<ColorKey> written = [];
        foreach (ColorKey kept in keys)
        {
            if (MathF.Abs(kept.Time - time) < SameTime)
            {
                key.Mode = kept.Mode;
            }
            else
            {
                written.Add(kept);
            }
        }

        written.Add(key);
        ColorCurve curve = new(written);
        _environment.Colors[name] = curve;
        return curve;
    }

    private CurveTrack? FindTrack(string name) => Curves.Tracks.FirstOrDefault(track => track.Name == name);

    private void HandleSelected(EnvironmentValueViewModel value)
    {
        if (value.Value is MoodColor)
        {
            ShowColor(value);
        }
        else
        {
            ShowNumber(value);
        }
    }

    private void ShowNumber(EnvironmentValueViewModel value)
    {
        CurveTrack? shown = FindTrack(value.Name);
        if (!value.IsSelected)
        {
            if (shown is not null)
            {
                Curves.Tracks.Remove(shown);
                if (ReferenceEquals(Curves.Active, shown))
                {
                    Curves.Active = Curves.Tracks.LastOrDefault();
                }
            }

            return;
        }

        if (shown is not null)
        {
            return;
        }

        Curve curve = _environment.Numbers.GetValueOrDefault(value.Name) ?? new Curve();
        CurveTrack track = new(value.Name, curve, edited => HandleCurveCommitted(value.Name, edited), Day);
        Curves.Tracks.Add(track);
        Curves.Active = track;
    }

    private void ShowColor(EnvironmentValueViewModel value)
    {
        ColorTrack? shown = ColorTracks.FirstOrDefault(track => track.Name == value.Name);
        if (!value.IsSelected)
        {
            if (shown is not null)
            {
                ColorTracks.Remove(shown);
            }

            return;
        }

        if (shown is not null)
        {
            return;
        }

        ColorCurve curve = _environment.Colors.GetValueOrDefault(value.Name) ?? new ColorCurve();
        ColorTrack track = new(value.Name, curve, edited => HandleColorCommitted(value.Name, edited), Day);
        ColorTracks.Add(track);
    }

    private void HandleCurveCommitted(string name, Curve curve)
    {
        _environment.Numbers[name] = curve;
        Refresh();
        _edited();
    }

    private void HandleColorCommitted(string name, ColorCurve curve)
    {
        _environment.Colors[name] = curve;
        Refresh();
        _edited();
    }

    /// <summary>Shows every value as the environment draws it at the hour, over the defaults for those it does not draw.</summary>
    private void Refresh()
    {
        _refreshing = true;
        try
        {
            foreach (EnvironmentValueViewModel value in _values.Values)
            {
                value.Value.Copy(Defaults, _atHour);
            }

            _environment.Apply(Hour, _atHour);
            foreach (EnvironmentValueViewModel value in _values.Values)
            {
                value.IsDrawn = IsDrawn(value.Name);
                value.Field.Refresh();
            }
        }
        finally
        {
            _refreshing = false;
        }
    }
}
