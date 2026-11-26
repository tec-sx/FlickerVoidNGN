using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Curves;

/// <summary>A drag under way in the curve view: the keys as they were when pressed, where the press was, and what it took.</summary>
internal sealed class CurveDrag(IReadOnlyList<CurveKey> keys, double startTime, double startValue, int handle, bool isOut)
{
    public const int NoHandle = -1;

    public IReadOnlyList<CurveKey> Keys { get; } = keys;

    public double StartTime { get; } = startTime;

    public double StartValue { get; } = startValue;

    /// <summary>The key whose slope handle was taken, or <see cref="NoHandle"/> when keys are moved.</summary>
    public int Handle { get; } = handle;

    /// <summary>Whether the handle taken is the one the key is left at.</summary>
    public bool Out { get; } = isOut;

    public bool Moved { get; set; }

    /// <summary>The selected keys, by their place among <see cref="Keys"/>.</summary>
    public HashSet<int> Selected { get; set; } = [];
}
