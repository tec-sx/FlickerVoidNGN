using Arch.Core;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>The interactable an interactor is focused on, such as the one under the mouse; its offers are the ones the player sees.</summary>
public readonly record struct InteractorFocus(Entity Target);
