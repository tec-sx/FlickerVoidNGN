using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;

namespace FlickerVoid.Engine.Maps.Definitions;

/// <summary>A map of an area: one or more layers, used while the navigator stands on one of them.</summary>
public sealed class MapDefinition : Definition
{
    /// <summary>Higher wins where maps overlap, so an interior's map replaces the town's around it.</summary>
    public int Priority { get; set; }

    /// <summary>The map is used only while these hold, such as once the player owns it.</summary>
    public ConditionSet AvailableWhen { get; set; } = new();

    public List<MapLayer> Layers { get; set; } = [];

    /// <summary>The layer covering a point, a height-limited one before one without a limit; -1 when none does.</summary>
    public int FindLayerAt(System.Numerics.Vector3 point)
    {
        int fallback = -1;
        for (int index = 0; index < Layers.Count; index++)
        {
            MapLayer layer = Layers[index];
            if (!layer.Contains(point))
            {
                continue;
            }

            if (layer.LimitHeight)
            {
                return index;
            }

            if (fallback < 0)
            {
                fallback = index;
            }
        }

        return fallback;
    }

    /// <summary>The first layer without a height limit over the point's ground, such as the street around an interior; -1 when none.</summary>
    public int FindBaseLayerAt(System.Numerics.Vector3 point) => Layers.FindIndex(layer => !layer.LimitHeight && layer.ContainsGround(point));

    public int FindLayerByName(string name) => Layers.FindIndex(layer => layer.Name == name);

    public MapLayer? GetLayer(int index)
    {
        if (index < 0 || index >= Layers.Count)
        {
            return null;
        }

        return Layers[index];
    }
}
