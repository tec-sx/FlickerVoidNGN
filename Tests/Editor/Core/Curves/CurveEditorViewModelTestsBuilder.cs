using FlickerVoid.Editor.Core.Curves;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Tests.Curves;

/// <summary>
/// Builds a curve editor on a 240 by 100 pixel view of a day and values from 0 to 10 (10 pixels an hour and a unit), its
/// tracks keeping the curves they commit.
/// </summary>
internal sealed class CurveEditorViewModelTestsBuilder
{
    private readonly CurveEditorViewModel _editor = new();

    public CurveEditorViewModelTestsBuilder()
    {
        _editor.Range.Resize(240, 100);
        _editor.Range.Frame(0, 24, 0, 10, margin: 0);
    }

    public List<Curve> Commits { get; } = [];

    /// <summary>Adds a track of the keys, given as hour and value pairs, made active; looping over a day unless told not to.</summary>
    public CurveTrack WithTrack(params (float Time, float Value)[] keys)
    {
        return WithTrack(LightingDay, keys);
    }

    public CurveTrack WithTrack(float period, params (float Time, float Value)[] keys)
    {
        CurveKey[] built = Array.ConvertAll(keys, key => new CurveKey { Time = key.Time, Value = key.Value });
        Curve curve = new(built);
        CurveTrack track = new("Exposure", curve, Commits.Add, period);
        _editor.Tracks.Add(track);
        _editor.Active = track;
        return track;
    }

    public CurveTrack WithTrack(Curve curve)
    {
        CurveTrack track = new("Exposure", curve, Commits.Add, LightingDay);
        _editor.Tracks.Add(track);
        _editor.Active = track;
        return track;
    }

    public CurveEditorViewModel Build() => _editor;

    private const float LightingDay = 24;
}
