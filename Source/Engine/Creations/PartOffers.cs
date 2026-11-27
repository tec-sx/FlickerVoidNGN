using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// Lets interactors use the parts of creations that have a toggle and no interactable of their own, such as a door the door tool
/// hung: while the game plays, each offers its toggle's action on <see cref="Input"/>. A game service; a game with another use
/// input sets it before it plays.
/// </summary>
public sealed class PartOffers
{
    private readonly World _world;
    private readonly HashSet<Entity> _offered = [];

    public PartOffers(World world, EntityLifecycle lifecycle)
    {
        _world = world;
        lifecycle.OnActivate((Entity entity, ref Toggle toggle) => Offer(entity, toggle));
        lifecycle.OnDeactivate((Entity entity, ref Toggle toggle) => Withdraw(entity));
    }

    /// <summary>The input action that uses a part.</summary>
    public Tag Input { get; set; } = new("Input.Use");

    /// <summary>The name prompts show for the offer.</summary>
    public string Name { get; set; } = "Use";

    private void Offer(Entity entity, in Toggle toggle)
    {
        if (!toggle.Action.IsValid || !_world.Has<CreationPart>(entity) || _world.Has<Interactable>(entity) || _world.Has<ActiveInteractable>(entity))
        {
            return;
        }

        InteractionOffer offer = new() { Input = Input, Action = toggle.Action };
        offer.Display.Name = Name;
        InteractableDefinition definition = new();
        definition.Offers.Add(offer);
        ActiveInteractable active = new() { Definition = definition, State = InteractableState.Awake, Uses = [offer.Uses] };
        _world.Add(entity, active);
        _offered.Add(entity);
    }

    private void Withdraw(Entity entity)
    {
        if (_offered.Remove(entity) && _world.Has<ActiveInteractable>(entity))
        {
            _world.Remove<ActiveInteractable>(entity);
        }
    }
}
