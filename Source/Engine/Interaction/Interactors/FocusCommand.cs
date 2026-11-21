using Arch.Core;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>Focuses an interactor on an interactable, or on nothing with <see cref="Entity.Null"/> or anything that isn't one.</summary>
public readonly record struct FocusCommand(Entity Interactor, Entity Target);
