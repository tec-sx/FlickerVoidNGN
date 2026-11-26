using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Rendering.Moods;

namespace FlickerVoid.Editor.Core.Environments;

/// <summary>
/// One value an environment can draw, as the environment editor lists it: its field at the cursor's hour, whether the
/// environment has a curve for it, and whether its curve is shown.
/// </summary>
public sealed partial class EnvironmentValueViewModel(MoodValue value, FieldViewModel field, Action<EnvironmentValueViewModel> selected)
    : ObservableObject
{
    public MoodValue Value { get; } = value;

    /// <summary>The value's name as files spell it, such as <c>fogColor</c>.</summary>
    public string Name => Value.Name;

    public string Label => DisplayNames.FromIdentifier(Value.Name);

    public bool IsColor => Value is MoodColor;

    /// <summary>The value at the cursor's hour; editing it writes a key there.</summary>
    public FieldViewModel Field { get; } = field;

    /// <summary>Whether the environment has a curve for the value; without one, what lies under the environment shows.</summary>
    [ObservableProperty]
    public partial bool IsDrawn { get; set; }

    /// <summary>Whether the value's curve is shown in the curve view, or its color in a strip.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Which of the shown curves is its own, counted from 0, so its dot takes the curve's colour; -1 while it shows none.</summary>
    [ObservableProperty]
    public partial int TrackIndex { get; set; } = NoTrack;

    public const int NoTrack = -1;

    partial void OnIsSelectedChanged(bool value) => selected(this);
}
