using Arch.Core;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>Sent when an interactor's focus moves, to <see cref="Entity.Null"/> when it lost it.</summary>
public readonly record struct FocusChangedEvent(Entity Interactor, Entity Target);
