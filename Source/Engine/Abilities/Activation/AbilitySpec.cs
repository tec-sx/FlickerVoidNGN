using Arch.Core;

using FlickerVoid.Engine.Abilities.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>An ability an entity was granted: its input, and while it runs, the target it acts on.</summary>
public sealed class AbilitySpec(AbilityDefinition ability, Tag input)
{
    public AbilityDefinition Ability { get; } = ability;

    public Tag Input { get; } = input;

    public bool IsActive { get; internal set; }

    /// <summary>What it acts on while it runs, such as the door being picked; <see cref="Entity.Null"/> when nothing.</summary>
    public Entity Target { get; internal set; } = Entity.Null;
}
