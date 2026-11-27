using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>Puts its entity on the maps, the minimap and the compass, as its marker definition describes.</summary>
public struct MapMarker
{
    public MapMarker()
    {
    }

    [ContentFile(".fvdata")]
    public string Definition { get; set; } = string.Empty;

    /// <summary>The name of this place, such as the inn's; empty uses the definition's name.</summary>
    public LocalizedText Label { get; set; } = new();

    /// <summary>Set to 1 once the place is discovered; needed by markers with a discovery fragment to keep it.</summary>
    public Tag DiscoveredFact { get; set; }

    public bool Enabled { get; set; } = true;
}
