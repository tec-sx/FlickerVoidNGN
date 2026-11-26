using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Curves;

/// <summary>
/// Number curves overlaid on one view, the active one edited with the pointer: keys selected, dragged (times snapped to
/// <see cref="TimeSnap"/>), added on the line, deleted, given a mode, and a free key's slopes turned by their handles. A drag
/// is committed once, when it is released; every other edit at once.
/// </summary>
public sealed partial class CurveEditorViewModel : ObservableObject
{
    /// <summary>How far from a key or handle, in pixels, a press still takes it.</summary>
    public const double PickRadius = 7;

    /// <summary>How long a free key's slope handles are drawn, in pixels.</summary>
    public const double HandleLength = 40;

    /// <summary>The time either side of a key over which the slope it had is measured when it is made free.</summary>
    private const float SlopeStep = 1e-3f;

    /// <summary>The samples taken across the view to find the values a fit must show.</summary>
    private const int FitSamples = 97;

    private readonly List<int> _selected = [];
    private CurveDrag? _drag;

    public CurveRange Range { get; } = new();

    public ObservableCollection<CurveTrack> Tracks { get; } = [];

    /// <summary>The track the pointer edits; changing it clears the selection.</summary>
    [ObservableProperty]
    public partial CurveTrack? Active { get; set; }

    /// <summary>The step key times snap to while dragged or added: a quarter of an hour.</summary>
    public float TimeSnap { get; set; } = 0.25f;

    /// <summary>The active track's selected keys, by their place in time order.</summary>
    public IReadOnlyList<int> SelectedKeys => _selected;

    /// <summary>
    /// Takes the active track's slope handle or key under the point: a key is selected alone, or with <paramref name="extend"/>
    /// added to the selection or taken out of it; empty space clears the selection. True when something was taken.
    /// </summary>
    public bool Press(double x, double y, bool extend)
    {
        _drag = null;
        if (Active is not { } track)
        {
            return false;
        }

        if (FindHandle(track, x, y) is { } handle)
        {
            _drag = new CurveDrag(track.Curve.Keys, Range.ToTime(x), Range.ToValue(y), handle.Key, handle.Out);
            return true;
        }

        int? key = FindKey(track, x, y);
        if (key is not int found)
        {
            if (!extend)
            {
                Select([]);
            }

            return false;
        }

        bool selected = _selected.Contains(found);
        if (extend && selected)
        {
            List<int> rest = [.. _selected];
            rest.Remove(found);
            Select(rest);
            return true;
        }

        if (extend)
        {
            Select([.. _selected, found]);
        }
        else if (!selected)
        {
            Select([found]);
        }

        _drag = new CurveDrag(track.Curve.Keys, Range.ToTime(x), Range.ToValue(y), CurveDrag.NoHandle, false)
        {
            Selected = [.. _selected],
        };
        return true;
    }

    /// <summary>Moves what the press took to the point, shown but not yet committed; <paramref name="snap"/> snaps key times.</summary>
    public void Drag(double x, double y, bool snap)
    {
        if (_drag is not { } drag || Active is not { } track)
        {
            return;
        }

        if (drag.Handle != CurveDrag.NoHandle)
        {
            TurnHandle(track, drag, x, y);
            return;
        }

        double time = Range.ToTime(x) - drag.StartTime;
        double value = Range.ToValue(y) - drag.StartValue;
        List<(CurveKey Key, bool Selected)> keys = [];
        for (int i = 0; i < drag.Keys.Count; i++)
        {
            CurveKey key = drag.Keys[i];
            bool selected = drag.Selected.Contains(i);
            if (selected)
            {
                float moved = key.Time + (float)time;
                key.Time = PlaceTime(track, moved, snap);
                key.Value += (float)value;
            }

            keys.Add((key, selected));
        }

        Show(track, keys);
        drag.Moved = true;
    }

    /// <summary>Ends a drag, committing the track when it moved.</summary>
    public void Release()
    {
        if (_drag is { Moved: true } && Active is { } track)
        {
            track.Commit();
        }

        _drag = null;
    }

    /// <summary>Adds a key to the active track at the snapped time under the point, on its line, or at the point when it has no keys.</summary>
    public void AddKey(double x, double y)
    {
        if (Active is not { } track)
        {
            return;
        }

        double pointer = Range.ToTime(x);
        float time = PlaceTime(track, (float)pointer, snap: true);
        float value = (float)Range.ToValue(y);
        if (track.Curve.Keys.Count > 0)
        {
            value = track.Evaluate(time);
        }

        List<(CurveKey Key, bool Selected)> keys = [.. track.Curve.Keys.Select(key => (key, false))];
        CurveKey added = new() { Time = time, Value = value };
        keys.Add((added, true));
        Show(track, keys);
        track.Commit();
    }

    public void DeleteSelected()
    {
        if (Active is not { } track || _selected.Count == 0)
        {
            return;
        }

        List<(CurveKey Key, bool Selected)> keys = [];
        for (int i = 0; i < track.Curve.Keys.Count; i++)
        {
            if (!_selected.Contains(i))
            {
                keys.Add((track.Curve.Keys[i], false));
            }
        }

        Show(track, keys);
        track.Commit();
    }

    /// <summary>Sets the selected keys' mode; a key made free keeps the slopes it had, so the curve does not change.</summary>
    public void SetMode(CurveMode mode)
    {
        if (Active is not { } track || _selected.Count == 0)
        {
            return;
        }

        List<(CurveKey Key, bool Selected)> keys = [];
        for (int i = 0; i < track.Curve.Keys.Count; i++)
        {
            CurveKey key = track.Curve.Keys[i];
            bool selected = _selected.Contains(i);
            if (selected && mode == CurveMode.Free && key.Mode != CurveMode.Free)
            {
                float at = track.Evaluate(key.Time);
                float before = track.Evaluate(key.Time - SlopeStep);
                float after = track.Evaluate(key.Time + SlopeStep);
                key.In = (at - before) / SlopeStep;
                key.Out = (after - at) / SlopeStep;
            }

            if (selected)
            {
                key.Mode = mode;
            }

            keys.Add((key, selected));
        }

        Show(track, keys);
        track.Commit();
    }

    /// <summary>Where a key of the active track's slope handles are drawn; null unless it is a free key.</summary>
    public CurveHandles? GetHandles(int index)
    {
        if (Active is not { } track || index < 0 || index >= track.Curve.Keys.Count)
        {
            return null;
        }

        CurveKey key = track.Curve.Keys[index];
        if (key.Mode != CurveMode.Free)
        {
            return null;
        }

        CurvePoint at = Range.ToScreen(key.Time, key.Value);
        CurvePoint arrive = GetHandleEnd(at, key.In, -1);
        CurvePoint leave = GetHandleEnd(at, key.Out, 1);
        return new CurveHandles(arrive, leave);
    }

    /// <summary>The track's line across the whole view, looping curves drawn past the ends of their period.</summary>
    public IReadOnlyList<CurvePoint> GetLine(CurveTrack track, int samples)
    {
        int count = Math.Max(samples, 2);
        CurvePoint[] line = new CurvePoint[count];
        for (int i = 0; i < count; i++)
        {
            double x = Range.Width * i / (count - 1);
            double time = Range.ToTime(x);
            float value = track.Evaluate((float)time);
            line[i] = Range.ToScreen(time, value);
        }

        return line;
    }

    /// <summary>Frames every track: the whole period of looping ones, else their keys' times, and all the values their lines reach.</summary>
    public void Fit()
    {
        (double minTime, double maxTime) = GetFitTimes();
        double minValue = double.MaxValue;
        double maxValue = double.MinValue;
        foreach (CurveTrack track in Tracks)
        {
            if (track.Curve.Keys.Count == 0)
            {
                continue;
            }

            for (int i = 0; i < FitSamples; i++)
            {
                double time = minTime + ((maxTime - minTime) * i / (FitSamples - 1));
                float value = track.Evaluate((float)time);
                minValue = Math.Min(minValue, value);
                maxValue = Math.Max(maxValue, value);
            }

            foreach (CurveKey key in track.Curve.Keys)
            {
                minValue = Math.Min(minValue, key.Value);
                maxValue = Math.Max(maxValue, key.Value);
            }
        }

        if (minValue > maxValue)
        {
            minValue = 0;
            maxValue = 1;
        }

        Range.Frame(minTime, maxTime, minValue, maxValue);
    }

    partial void OnActiveChanged(CurveTrack? value)
    {
        _drag = null;
        Select([]);
    }

    private (double Min, double Max) GetFitTimes()
    {
        float period = Tracks.Count == 0 ? 24 : Tracks.Max(track => track.Period);
        if (period > 0)
        {
            return (0, period);
        }

        IEnumerable<float> times = Tracks.SelectMany(track => track.Curve.Keys).Select(key => key.Time);
        float[] all = [.. times];
        if (all.Length == 0)
        {
            return (0, 1);
        }

        return (all.Min(), all.Max());
    }

    private void TurnHandle(CurveTrack track, CurveDrag drag, double x, double y)
    {
        CurveKey key = drag.Keys[drag.Handle];
        double time = Range.ToTime(x) - key.Time;
        double value = Range.ToValue(y) - key.Value;
        bool wrongSide = drag.Out ? time <= 0 : time >= 0;
        if (wrongSide || Math.Abs(time) < 1e-6)
        {
            return;
        }

        float slope = (float)(value / time);
        if (drag.Out)
        {
            key.Out = slope;
        }
        else
        {
            key.In = slope;
        }

        List<(CurveKey Key, bool Selected)> keys = [];
        for (int i = 0; i < drag.Keys.Count; i++)
        {
            CurveKey shown = i == drag.Handle ? key : drag.Keys[i];
            keys.Add((shown, _selected.Contains(i)));
        }

        Show(track, keys);
        drag.Moved = true;
    }

    private CurvePoint GetHandleEnd(CurvePoint at, float slope, int side)
    {
        double across = Range.Width / (Range.MaxTime - Range.MinTime);
        double up = Range.Height / (Range.MaxValue - Range.MinValue);
        double x = across;
        double y = -slope * up;
        double length = Math.Sqrt((x * x) + (y * y));
        double scale = side * HandleLength / length;
        return new CurvePoint(at.X + (x * scale), at.Y + (y * scale));
    }

    private (int Key, bool Out)? FindHandle(CurveTrack track, double x, double y)
    {
        for (int i = 0; i < track.Curve.Keys.Count; i++)
        {
            if (GetHandles(i) is not { } handles)
            {
                continue;
            }

            if (IsNear(handles.Out, x, y))
            {
                return (i, true);
            }

            if (IsNear(handles.In, x, y))
            {
                return (i, false);
            }
        }

        return null;
    }

    private int? FindKey(CurveTrack track, double x, double y)
    {
        int? nearest = null;
        double best = PickRadius * PickRadius;
        for (int i = 0; i < track.Curve.Keys.Count; i++)
        {
            CurveKey key = track.Curve.Keys[i];
            CurvePoint point = Range.ToScreen(key.Time, key.Value);
            double dx = point.X - x;
            double dy = point.Y - y;
            double distance = (dx * dx) + (dy * dy);
            if (distance <= best)
            {
                best = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    private static bool IsNear(CurvePoint point, double x, double y)
    {
        double dx = point.X - x;
        double dy = point.Y - y;
        return (dx * dx) + (dy * dy) <= PickRadius * PickRadius;
    }

    private float PlaceTime(CurveTrack track, float time, bool snap) => CurveTimes.Place(time, track.Period, TimeSnap, snap);

    /// <summary>Shows the keys in time order, the selection following the keys it marks.</summary>
    private void Show(CurveTrack track, List<(CurveKey Key, bool Selected)> keys)
    {
        List<(CurveKey Key, bool Selected)> ordered = [.. keys.OrderBy(entry => entry.Key.Time)];
        CurveKey[] shown = [.. ordered.Select(entry => entry.Key)];
        List<int> selected = [];
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Selected)
            {
                selected.Add(i);
            }
        }

        track.Show(shown);
        Select(selected);
    }

    private void Select(List<int> keys)
    {
        keys.Sort();
        _selected.Clear();
        _selected.AddRange(keys.Distinct());
        OnPropertyChanged(nameof(SelectedKeys));
    }
}
