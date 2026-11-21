using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>An input of an interactor's: a press starts the focused interactable's offer for it, and while one is under way its gesture follows the input.</summary>
public readonly record struct InteractionInput(Entity Interactor, Tag Input, InputPhase Phase);
