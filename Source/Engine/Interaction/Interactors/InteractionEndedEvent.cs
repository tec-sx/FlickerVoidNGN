using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>Sent when an interaction ends, how it ended saying whether it succeeded.</summary>
public readonly record struct InteractionEndedEvent(Entity Interactor, Entity Target, Tag Action, InteractionEnd End)
{
    public bool Succeeded => End == InteractionEnd.Succeeded;
}
