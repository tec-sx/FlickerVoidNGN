namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>
/// What a unit of a timeline's time means: where its lines and labels fall at a zoom, what a drag snaps to, how a time reads and
/// whether time wraps. A strategy, so a new unit (animation frames) is a new class and the controls stay as they are.
/// </summary>
public interface ITimeScale
{
    /// <summary>The unit's name, shown beside the ruler.</summary>
    string Unit { get; }

    /// <summary>The length after which time repeats (24 for hours), or null when it does not.</summary>
    double? Period { get; }

    /// <summary>Fills the list with the lines and labels within the shown span, in time order, as far apart as reads well.</summary>
    void GetTicks(TimeRange range, List<TimeTick> ticks);

    /// <summary>The step a drag snaps to at the range's zoom.</summary>
    double GetSnapStep(TimeRange range);

    /// <summary>A time as the position text and the playhead's chip show it.</summary>
    string Format(double time);
}
