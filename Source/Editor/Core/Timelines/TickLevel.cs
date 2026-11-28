namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>How strongly a tick of a time scale is drawn: a section start (a phrase, midnight), a labelled step, or a line between.</summary>
public enum TickLevel
{
    Minor,
    Major,
    Section,
}
