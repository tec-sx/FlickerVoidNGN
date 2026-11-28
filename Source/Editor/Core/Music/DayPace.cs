namespace FlickerVoid.Editor.Core.Music;

/// <summary>How fast the day passes while an arrangement without a tempo plays in the editor: hours of the ruler to a second, 0 holding the hour.</summary>
public sealed record DayPace(string Label, double HoursPerSecond)
{
    public override string ToString() => Label;
}
