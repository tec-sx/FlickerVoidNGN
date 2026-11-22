using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>A place the navigator discovers by coming near; the discovery is kept in the marker's fact.</summary>
public sealed class DiscoveryFragment : MarkerFragment
{
    /// <summary>Metres within which the navigator discovers it.</summary>
    public float Radius { get; set; } = 20;

    /// <summary>Left out of every view until discovered; otherwise it shows as undiscovered, with <see cref="UndiscoveredIcon"/>.</summary>
    public bool HiddenUntilDiscovered { get; set; }

    [ContentFile(".png")]
    public string UndiscoveredIcon { get; set; } = string.Empty;

    /// <summary>Applied with the discoverer as instigator and the marker's entity as target.</summary>
    public EffectList OnDiscovered { get; set; } = new();
}
