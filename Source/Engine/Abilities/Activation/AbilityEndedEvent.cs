using Arch.Core;

using FlickerVoid.Engine.Abilities.Definitions;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>An ability stopped running: ended when it finished, cancelled when something cut it short.</summary>
public readonly record struct AbilityEndedEvent(Entity Owner, AbilityDefinition Ability, Entity Target, bool Cancelled);
