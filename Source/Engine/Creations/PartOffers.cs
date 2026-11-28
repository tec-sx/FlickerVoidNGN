using Arch.Core;

using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// Lets interactors use the parts of creations that have a toggle and no interactable of their own, such as a door the door tool
/// hung: while the game plays, each offers its toggle's action on <see cref="Input"/>. A part that follows another
/// (<see cref="PartFollower"/>, the second leaf of a double door) offers its leader's action, and using it uses the leader. A game
/// service; a game with another use input sets it before it plays.
/// </summary>
public sealed class PartOffers
{
    private readonly World _world;
    private readonly HashSet<Entity> _offered = [];

    private static readonly QueryDescription Toggles = new QueryDescription().WithAll<CreationPart, Toggle>();

    private readonly EventBus _events;

    public PartOffers(World world, EntityLifecycle lifecycle, EventBus events)
    {
        _world = world;
        _events = events;
        lifecycle.OnActivate((Entity entity, ref Toggle toggle) => Offer(entity, toggle));
        lifecycle.OnDeactivate((Entity entity, ref Toggle toggle) => Withdraw(entity));
        lifecycle.OnActivate((Entity entity, ref PartFollower follower) => OfferLeader(entity));
        lifecycle.OnDeactivate((Entity entity, ref PartFollower follower) => Withdraw(entity));
        events.Handle((in InteractionActionEvent action) => Forward(action));
    }

    /// <summary>The input action that uses a part.</summary>
    public Tag Input { get; set; } = new("Input.Use");

    /// <summary>The name prompts show for the offer.</summary>
    public LocalizedText Name { get; set; } = new("Creation.Part.Use", "Use", "The prompt's button for using a part of a creation, such as opening a door.");

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

    /// <summary>The part that the follower stands in for, or <see cref="Entity.Null"/>.</summary>
    public Entity FindLeader(Entity follower)
    {
        Entity leader = Entity.Null;
        if (!_world.IsAlive(follower) || !_world.TryGet(follower, out PartFollower follows) || !_world.TryGet(follower, out CreationPart own))
        {
            return leader;
        }

        _world.Query(in Toggles, (Entity entity, ref CreationPart part) =>
        {
            if (part.Root == own.Root && part.Id == follows.Leader)
            {
                leader = entity;
            }
        });
        return leader;
    }

    private void OfferLeader(Entity entity)
    {
        Entity leader = FindLeader(entity);
        if (leader != Entity.Null && !_world.Has<Toggle>(entity))
        {
            Offer(entity, _world.Get<Toggle>(leader));
        }
    }

    // An interaction with a follower is done to its leader.
    private void Forward(in InteractionActionEvent action)
    {
        Entity leader = FindLeader(action.Target);
        if (leader != Entity.Null)
        {
            _events.Send(new InteractionActionEvent(action.Interactor, leader, action.Action));
        }
    }

    private void Withdraw(Entity entity)
    {
        if (_offered.Remove(entity) && _world.Has<ActiveInteractable>(entity))
        {
            _world.Remove<ActiveInteractable>(entity);
        }
    }
}
