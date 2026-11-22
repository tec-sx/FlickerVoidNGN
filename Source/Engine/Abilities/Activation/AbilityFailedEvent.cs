using Arch.Core;

using FlickerVoid.Engine.Abilities.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>An ability was asked to activate and couldn't, for an <see cref="AbilityFailure"/> reason, with the message its definition gives for it.</summary>
public readonly record struct AbilityFailedEvent(Entity Owner, AbilityDefinition Ability, Tag Reason, string Message);
