using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Curves;

/// <summary>
/// A color curve on its gradient strip, edited along time: one key selected, dragged (times snapped to
/// <see cref="TimeSnap"/>), added with the color already there, recolored, given a mode or deleted. A drag is committed to
/// <c>committed</c> once, when it is released; every other edit at once.
/// </summary>
public sealed partial class ColorTrack(string name, ColorCurve curve, Action<ColorCurve> committed, float period = 0) : ObservableObject
{
    private IReadOnlyList<ColorKey>? _dragKeys;
    private int _dragKey;
    private bool _moved;

    public string Name { get; } = name;

    public float Period { get; } = period;

    /// <summary>The curve as shown, which may be ahead of the one last committed while a key is dragged.</summary>
    [ObservableProperty]
    public partial ColorCurve Curve { get; private set; } = curve;

    /// <summary>The selected key, by its place in time order.</summary>
    [ObservableProperty]
    public partial int? Selected { get; private set; }

    public float TimeSnap { get; set; } = 0.25f;

    public Vector3 Evaluate(float time) => Curve.Evaluate(time, Period);

    /// <summary>Selects the key nearest the time within <paramref name="reach"/> and starts dragging it, or clears the selection.</summary>
    public bool Press(double time, double reach)
    {
        _dragKeys = null;
        int? nearest = null;
        double best = reach;
        for (int i = 0; i < Curve.Keys.Count; i++)
        {
            double distance = Math.Abs(Curve.Keys[i].Time - time);
            if (distance <= best)
            {
                best = distance;
                nearest = i;
            }
        }

        Selected = nearest;
        if (nearest is not int key)
        {
            return false;
        }

        _dragKeys = Curve.Keys;
        _dragKey = key;
        _moved = false;
        return true;
    }

    /// <summary>Moves the pressed key to the time, shown but not yet committed.</summary>
    public void Drag(double time, bool snap)
    {
        if (_dragKeys is null)
        {
            return;
        }

        List<(ColorKey Key, bool Selected)> keys = [];
        for (int i = 0; i < _dragKeys.Count; i++)
        {
            ColorKey key = _dragKeys[i];
            if (i == _dragKey)
            {
                key.Time = CurveTimes.Place((float)time, Period, TimeSnap, snap);
            }

            keys.Add((key, i == _dragKey));
        }

        Show(keys);
        _moved = true;
    }

    /// <summary>Ends a drag, committing the curve when the key moved.</summary>
    public void Release()
    {
        if (_dragKeys is not null && _moved)
        {
            committed(Curve);
        }

        _dragKeys = null;
    }

    /// <summary>Adds a key at the snapped time with the color the curve has there, selected.</summary>
    public void AddKey(double time)
    {
        float placed = CurveTimes.Place((float)time, Period, TimeSnap, snap: true);
        Vector3 color = Evaluate(placed);
        List<(ColorKey Key, bool Selected)> keys = [.. Curve.Keys.Select(key => (key, false))];
        keys.Add((new ColorKey { Time = placed, Color = color }, true));
        Show(keys);
        committed(Curve);
    }

    public void SetColor(Vector3 color) => ChangeSelected(key => key with { Color = color });

    public void SetMode(CurveMode mode) => ChangeSelected(key => key with { Mode = mode });

    public void DeleteSelected()
    {
        if (Selected is not int selected)
        {
            return;
        }

        List<(ColorKey Key, bool Selected)> keys = [];
        for (int i = 0; i < Curve.Keys.Count; i++)
        {
            if (i != selected)
            {
                keys.Add((Curve.Keys[i], false));
            }
        }

        Show(keys);
        committed(Curve);
    }

    /// <summary>Takes a curve its owner changed, such as after an undo, without committing it back.</summary>
    public void Reset(ColorCurve changed)
    {
        _dragKeys = null;
        Selected = null;
        Curve = changed;
    }

    private void ChangeSelected(Func<ColorKey, ColorKey> change)
    {
        if (Selected is not int selected)
        {
            return;
        }

        ColorKey[] keys = [.. Curve.Keys];
        keys[selected] = change(keys[selected]);
        Curve = new ColorCurve(keys);
        committed(Curve);
    }

    private void Show(List<(ColorKey Key, bool Selected)> keys)
    {
        List<(ColorKey Key, bool Selected)> ordered = [.. keys.OrderBy(entry => entry.Key.Time)];
        ColorKey[] shown = [.. ordered.Select(entry => entry.Key)];
        int index = ordered.FindIndex(entry => entry.Selected);
        Curve = new ColorCurve(shown);
        Selected = index < 0 ? null : index;
    }
}
