using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>
/// A kind of map marker (a shop, a quest target, the waypoint): its name from its display, its category from its tags and the views it
/// shows in from its fragments.
/// </summary>
public sealed class MarkerDefinition : Definition
{
    [ContentFile(".png")]
    public string Icon { get; set; } = string.Empty;

    /// <summary>Higher draws on top.</summary>
    public int Priority { get; set; }

    /// <summary>Checked with the navigator as instigator and the marker's entity as target, again whenever a fact changes.</summary>
    public ConditionSet VisibleWhen { get; set; } = new();

    public List<MarkerFragment> Fragments { get; set; } = [];

    public T? FindFragment<T>()
        where T : MarkerFragment
    {
        return Fragments.OfType<T>().FirstOrDefault();
    }
}
