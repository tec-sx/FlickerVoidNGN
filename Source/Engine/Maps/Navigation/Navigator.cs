using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>The player's view of the maps, on the protagonist: it follows the map underfoot, discovers places it comes near and has the minimap and compass the HUD draws.</summary>
public struct Navigator
{
    public Navigator()
    {
    }

    public MinimapSettings Minimap { get; set; } = new();

    public CompassSettings Compass { get; set; } = new();

    /// <summary>Marker categories (definition tags) left out of every view, such as those switched off in the map's legend; tracked markers still show.</summary>
    public List<Tag> HiddenCategories { get; set; } = [];
}
