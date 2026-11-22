using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Interface.Hud.Minimap;

/// <summary>A marker on the minimap: its offset from the centre in pixels (+Y down), and whether the player follows it or it is pinned to the edge.</summary>
public sealed partial class MinimapMarkerViewModel : ObservableObject
{
    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    public partial bool IsTracked { get; set; }

    [ObservableProperty]
    public partial bool IsClamped { get; set; }
}
