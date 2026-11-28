namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>Whether a region plays now, waits for its beat or is idle.</summary>
public enum RegionState
{
    Idle,
    Live,
    Queued,
}
