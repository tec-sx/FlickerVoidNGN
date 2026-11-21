using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>Sent when an interaction succeeds: responses on the target, such as toggles and locks, do what the action names.</summary>
public readonly record struct InteractionActionEvent(Entity Interactor, Entity Target, Tag Action);
