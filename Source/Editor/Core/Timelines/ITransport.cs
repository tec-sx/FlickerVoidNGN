using System.ComponentModel;

namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>What a transport bar plays and shows: whether it plays and loops, where it is, and play, pause and stop.</summary>
public interface ITransport : INotifyPropertyChanged
{
    bool IsPlaying { get; }

    /// <summary>Whether playing goes round at the end instead of stopping.</summary>
    bool IsLooping { get; set; }

    /// <summary>The position in the timeline's own unit ("6.2.1", "18:00").</summary>
    string PositionText { get; }

    /// <summary>The position as clock time ("0:13.1"), or empty.</summary>
    string TimeText { get; }

    /// <summary>Plays from the cursor, or pauses where it is.</summary>
    void TogglePlay();

    void Stop();
}
