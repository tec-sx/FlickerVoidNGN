using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>
/// One row of the rehearsal: a music area (or a script) of the game and the bars, set by dragging, during which it is taken as
/// holding, so the tracks it asks for are requested of the cue that plays.
/// </summary>
public sealed partial class RehearsalRow(string name, IReadOnlyList<string> layers, bool isArea, RehearsalSpan span) : ObservableObject
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Layers { get; } = layers;

    /// <summary>Whether a music area of the scene asks, as against a script.</summary>
    public bool IsArea { get; } = isArea;

    [ObservableProperty]
    private RehearsalSpan _span = span;

    /// <summary>Whether the playhead stood in the span at the last tick, so its layers are asked for.</summary>
    public bool Asking { get; set; }

    public string Label => IsArea ? $"Rehearsal · {Name}" : $"Rehearsal · {string.Join(", ", Layers)}";

    public string Hint => IsArea ? $"the fox is in {Name}" : $"a script asks for {string.Join(", ", Layers)}";
}
