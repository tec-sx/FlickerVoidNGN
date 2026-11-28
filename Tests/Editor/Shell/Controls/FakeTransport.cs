using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Tests.Controls;

/// <summary>A transport that counts its presses.</summary>
internal sealed partial class FakeTransport : ObservableObject, ITransport
{
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial bool IsLooping { get; set; }

    public string PositionText => "6.2.1";

    public string TimeText => "0:13.1";

    public int Stops { get; private set; }

    public void TogglePlay() => IsPlaying = !IsPlaying;

    public void Stop()
    {
        Stops++;
        IsPlaying = false;
    }
}
