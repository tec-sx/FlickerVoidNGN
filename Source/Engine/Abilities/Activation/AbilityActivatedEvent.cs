using Arch.Core;

using FlickerVoid.Engine.Abilities.Definitions;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>An ability activated, with its costs paid; systems that give it behaviour start here and end it with <see cref="AbilitySystem.End"/>.</summary>
public readonly record struct AbilityActivatedEvent(Entity Owner, AbilityDefinition Ability, Entity Target);
