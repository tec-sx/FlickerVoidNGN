using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>Sent when an interactor starts an offer's gesture on an interactable.</summary>
public readonly record struct InteractionStartedEvent(Entity Interactor, Entity Target, Tag Action);
