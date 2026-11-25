namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>The world clock's day and hour when the game was saved, to the minute the facts leave out.</summary>
public sealed class SavedClock
{
    public int Day { get; set; }

    public double Hour { get; set; }
}
