using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Curves;

/// <summary>
/// A number curve in the curve view: shown as it is edited, and handed to <c>committed</c> once an edit is done; with a
/// period (24 for a day) it loops and its keys stay inside it.
/// </summary>
public sealed partial class CurveTrack(string name, Curve curve, Action<Curve> committed, float period = 0) : ObservableObject
{
    public string Name { get; } = name;

    public float Period { get; } = period;

    /// <summary>The curve as shown, which may be ahead of the one last committed while a key is dragged.</summary>
    [ObservableProperty]
    public partial Curve Curve { get; private set; } = curve;

    public float Evaluate(float time) => Curve.Evaluate(time, Period);

    /// <summary>Shows keys without committing them, as a drag does.</summary>
    internal void Show(IReadOnlyList<CurveKey> keys) => Curve = new Curve(keys);

    internal void Commit() => committed(Curve);

    /// <summary>Takes a curve its owner changed, such as after an undo, without committing it back.</summary>
    public void Reset(Curve changed) => Curve = changed;
}
