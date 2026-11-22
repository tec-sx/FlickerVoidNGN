using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Shots;

/// <summary>A key on the shot timeline: its index, time, place along the timeline from 0 to 1, marker and whether it is selected.</summary>
public sealed partial class ShotKeyItem : ObservableObject
{
    public ShotKeyItem(int index, float time, float fraction, string marker)
    {
        Index = index;
        Time = time;
        Fraction = fraction;
        Marker = marker;
    }

    public int Index { get; }

    public float Time { get; }

    public float Fraction { get; }

    public string Marker { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
