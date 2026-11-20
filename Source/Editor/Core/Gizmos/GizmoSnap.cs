using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Gizmos;

/// <summary>How a gizmo handle snaps: whether it does, and the step it snaps to, one of <see cref="Steps"/>.</summary>
public sealed partial class GizmoSnap(IReadOnlyList<float> steps, float step) : ObservableObject
{
    /// <summary>The steps offered to pick from.</summary>
    public IReadOnlyList<float> Steps { get; } = steps;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial float Step { get; set; } = step;

    /// <summary>The value rounded to the nearest multiple of the step while snapping is on, or as it is.</summary>
    public float Apply(float value)
    {
        if (!IsOn || Step <= 0)
        {
            return value;
        }

        return MathF.Round(value / Step) * Step;
    }
}
