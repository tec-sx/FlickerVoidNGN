using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>An ability a set grants and the input that activates it.</summary>
public sealed class AbilitySetEntry
{
    [ContentFile(".fvdata")]
    public string Ability { get; set; } = string.Empty;

    /// <summary>The input action, such as <c>Input.Sneak</c>; none for abilities activated by rules or systems.</summary>
    public Tag Input { get; set; }
}
