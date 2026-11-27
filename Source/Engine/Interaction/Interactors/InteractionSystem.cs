using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Input.Gestures;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Interaction.Interactors;

/// <summary>
/// Runs interactions each fixed step: interactors focus interactables, a press of an offer's input within reach starts it, its gesture
/// decides how it ends, and a success does the offer's action, applies its effects and uses it up or rests the interactable.
/// </summary>
public sealed partial class InteractionSystem : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Interactables = new QueryDescription().WithAll<ActiveInteractable>();
    private static readonly QueryDescription Committed = new QueryDescription().WithAll<InteractionCommit>();

    private readonly EventBus _events;
    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly ILogger<InteractionSystem> _logger;
    private readonly List<Entity> _interacting = [];

    public InteractionSystem(Game game)
        : base(game.World)
    {
        _events = game.Events;
        _facts = game.Facts;
        _services = game.Services;
        _logger = game.LoggerFactory.CreateLogger<InteractionSystem>();
        ArrayRegistry.Add<ActiveInteractable>();
        ArrayRegistry.Add<UsePoint>();
        ArrayRegistry.Add<InteractorFocus>();
        ArrayRegistry.Add<InteractionCommit>();
        game.Lifecycle.OnActivate((Entity entity, ref Interactable interactable) => Activate(entity, interactable));
        game.Lifecycle.OnDeactivate((Entity entity, ref Interactable _) => Deactivate(entity));
        game.Lifecycle.OnDeactivate((Entity entity, ref Interactor _) => Stop(entity));
        _events.Handle((in FocusCommand command) => Focus(command.Interactor, command.Target));
        _events.Handle((in InteractionInput input) => Push(input));
        game.SaveStates.Add<SavedInteractable>("interactable", Save, Restore);
    }

    /// <summary>The offers of the interactor's focus it can see, heaviest first: hidden ones whose conditions fail are left out.</summary>
    public IReadOnlyList<OfferView> GetOffers(Entity interactor)
    {
        if (!TryGetFocus(interactor, out Entity target))
        {
            return [];
        }

        return GetOffers(interactor, target);
    }

    /// <summary>The offers of any interactable as the interactor sees them, such as the one under the mouse, heaviest first.</summary>
    public IReadOnlyList<OfferView> GetOffers(Entity interactor, Entity target)
    {
        List<OfferView> offers = [];
        if (!World.IsAlive(interactor) || !IsInteractable(target))
        {
            return offers;
        }

        ActiveInteractable active = World.Get<ActiveInteractable>(target);
        ConditionContext context = CreateContext(interactor, target);
        List<InteractionOffer> definitionOffers = active.Definition.Offers;
        for (int index = 0; index < definitionOffers.Count; index++)
        {
            InteractionOffer offer = definitionOffers[index];
            if (!offer.IsValid)
            {
                continue;
            }

            bool available = offer.Conditions.Evaluate(context);
            ConditionFailurePresentation presentation = offer.Conditions.FailurePresentation;
            if (!available && presentation == ConditionFailurePresentation.Hidden)
            {
                continue;
            }

            string reason = string.Empty;
            if (!available && presentation == ConditionFailurePresentation.ShowLockedWithReason)
            {
                reason = offer.Conditions.FailureReason;
                if (reason.Length == 0)
                {
                    reason = offer.Conditions.Describe();
                }
            }

            offers.Add(new OfferView(offer.Input, offer.Action, offer.Gesture, offer.Display, offer.Weight, active.Uses[index], available, reason));
        }

        offers.Sort(static (a, b) =>
        {
            if (a.Weight != b.Weight)
            {
                return b.Weight.CompareTo(a.Weight);
            }

            return string.CompareOrdinal(a.Input.Name, b.Input.Name);
        });
        return offers;
    }

    public override void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        World.Query(in Interactables, (ref ActiveInteractable active) => Rest(ref active, seconds));

        _interacting.Clear();
        World.Query(in Committed, (Entity entity) => _interacting.Add(entity));
        foreach (Entity interactor in _interacting)
        {
            Advance(interactor, seconds);
        }
    }

    private static void Rest(ref ActiveInteractable active, float seconds)
    {
        if (active.State != InteractableState.Cooldown)
        {
            return;
        }

        active.Cooldown -= seconds;
        if (active.Cooldown <= 0)
        {
            active.State = InteractableState.Awake;
        }
    }

    private void Activate(Entity entity, Interactable interactable)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null)
        {
            LogNoDataAssets(interactable.Definition);
            return;
        }

        InteractableDefinition definition;
        try
        {
            definition = data.Load<InteractableDefinition>(interactable.Definition);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogDefinitionFailed(interactable.Definition, exception.Message);
            return;
        }

        int[] uses = definition.Offers.Select(static offer => offer.Uses).ToArray();
        ActiveInteractable active = new() { Definition = definition, State = InteractableState.Awake, Uses = uses };
        World.Add(entity, active);
    }

    private SavedInteractable? Save(Entity entity)
    {
        if (!World.TryGet(entity, out ActiveInteractable active))
        {
            return null;
        }

        // An interaction under way is not saved: it is ready to be used again after a load.
        InteractableState state = active.State;
        if (state == InteractableState.Interacting)
        {
            state = InteractableState.Awake;
        }

        return new SavedInteractable { State = state, Uses = [.. active.Uses], Cooldown = active.Cooldown };
    }

    private void Restore(Entity entity, SavedInteractable saved)
    {
        if (!World.Has<ActiveInteractable>(entity))
        {
            return;
        }

        ref ActiveInteractable active = ref World.Get<ActiveInteractable>(entity);
        active.State = saved.State;
        active.Cooldown = saved.Cooldown;
        if (saved.Uses.Length == active.Uses.Length)
        {
            active.Uses = [.. saved.Uses];
        }
    }

    private void Deactivate(Entity entity)
    {
        if (World.Has<ActiveInteractable>(entity))
        {
            World.Remove<ActiveInteractable>(entity);
        }
    }

    private void Stop(Entity interactor)
    {
        if (World.TryGet(interactor, out InteractionCommit commit))
        {
            Finish(interactor, commit, InteractionEnd.Cancelled);
        }

        if (World.Has<InteractorFocus>(interactor))
        {
            World.Remove<InteractorFocus>(interactor);
        }
    }

    private void Focus(Entity interactor, Entity target)
    {
        if (!World.IsAlive(interactor) || !World.Has<Interactor>(interactor))
        {
            return;
        }

        if (!IsInteractable(target))
        {
            target = Entity.Null;
        }

        TryGetFocus(interactor, out Entity current);
        if (current == target)
        {
            return;
        }

        if (target == Entity.Null)
        {
            World.Remove<InteractorFocus>(interactor);
        }
        else if (World.Has<InteractorFocus>(interactor))
        {
            World.Set(interactor, new InteractorFocus(target));
        }
        else
        {
            World.Add(interactor, new InteractorFocus(target));
        }

        _events.Send(new FocusChangedEvent(interactor, target));
    }

    private void Push(in InteractionInput input)
    {
        Entity interactor = input.Interactor;
        if (!World.IsAlive(interactor))
        {
            return;
        }

        if (World.TryGet(interactor, out InteractionCommit commit))
        {
            commit.Gesture.Push(input.Input, input.Phase);
            return;
        }

        if (input.Phase == InputPhase.Pressed)
        {
            Begin(interactor, input.Input);
        }
    }

    private void Begin(Entity interactor, Tag input)
    {
        if (!World.TryGet(interactor, out Interactor settings) || !TryGetFocus(interactor, out Entity target))
        {
            return;
        }

        ref ActiveInteractable active = ref World.Get<ActiveInteractable>(target);
        if (!active.CanInteract || !active.Definition.Accepts(settings.Kind) || !IsInReach(interactor, target))
        {
            return;
        }

        int index = FindOffer(interactor, target, active, input);
        if (index < 0)
        {
            return;
        }

        InteractionOffer offer = active.Definition.Offers[index];
        GestureTracker gesture = new();
        gesture.Begin(input, offer.Gesture);
        gesture.Push(input, InputPhase.Pressed);
        active.State = InteractableState.Interacting;
        World.Add(interactor, new InteractionCommit(target, index, input, offer.Action, gesture));
        _events.Send(new InteractionStartedEvent(interactor, target, offer.Action));
        Advance(interactor, 0);
    }

    /// <summary>The heaviest offer for the input that can be done now, the first of equal weight; -1 when none can.</summary>
    private int FindOffer(Entity interactor, Entity target, in ActiveInteractable active, Tag input)
    {
        List<InteractionOffer> offers = active.Definition.Offers;
        int best = -1;
        for (int index = 0; index < offers.Count; index++)
        {
            InteractionOffer offer = offers[index];
            bool heavier = best < 0 || offer.Weight > offers[best].Weight;
            if (offer.IsValid && offer.Input == input && heavier && CanExecute(interactor, target, active, index))
            {
                best = index;
            }
        }

        return best;
    }

    private void Advance(Entity interactor, float seconds)
    {
        InteractionCommit commit = World.Get<InteractionCommit>(interactor);
        InteractionEnd? cancel = FindCancel(interactor, commit);
        if (cancel is { } reason)
        {
            Finish(interactor, commit, reason);
            return;
        }

        GestureStatus status = commit.Gesture.Update(seconds);
        if (status == GestureStatus.Completed)
        {
            Finish(interactor, commit, InteractionEnd.Succeeded);
        }
        else if (status != GestureStatus.Running)
        {
            GestureMode mode = commit.Gesture.Active?.Mode ?? GestureMode.Press;
            Finish(interactor, commit, GetFailure(mode));
        }
    }

    private static InteractionEnd GetFailure(GestureMode mode)
    {
        switch (mode)
        {
            case GestureMode.Hold:
                return InteractionEnd.Released;
            case GestureMode.Mash:
                return InteractionEnd.TimedOut;
            default:
                return InteractionEnd.Cancelled;
        }
    }

    private InteractionEnd? FindCancel(Entity interactor, in InteractionCommit commit)
    {
        Entity target = commit.Target;
        if (!IsInteractable(target))
        {
            return InteractionEnd.Removed;
        }

        if (!TryGetFocus(interactor, out Entity focus) || focus != target)
        {
            return InteractionEnd.FocusLost;
        }

        if (!IsInReach(interactor, target))
        {
            return InteractionEnd.OutOfReach;
        }

        ActiveInteractable active = World.Get<ActiveInteractable>(target);
        if (active.State != InteractableState.Interacting || !CanExecute(interactor, target, active, commit.Offer))
        {
            return InteractionEnd.RequirementFailed;
        }

        return null;
    }

    private void Finish(Entity interactor, InteractionCommit commit, InteractionEnd end)
    {
        commit.Gesture.Reset();
        World.Remove<InteractionCommit>(interactor);
        Entity target = commit.Target;
        bool succeeded = end == InteractionEnd.Succeeded;
        if (IsInteractable(target))
        {
            ref ActiveInteractable active = ref World.Get<ActiveInteractable>(target);
            if (succeeded)
            {
                _events.Send(new InteractionActionEvent(interactor, target, commit.Action));
                UseUp(ref active, commit.Offer);
            }

            if (active.State == InteractableState.Interacting)
            {
                active.State = InteractableState.Awake;
            }

            if (succeeded)
            {
                InteractionOffer offer = active.Definition.Offers[commit.Offer];
                ConditionContext context = CreateContext(interactor, target);
                offer.Effects.Apply(context);
            }
        }

        _events.Send(new InteractionEndedEvent(interactor, target, commit.Action, end));
    }

    /// <summary>Takes a use from the offer: the interactable completes once every offer is used up, and otherwise rests for its cooldown.</summary>
    private static void UseUp(ref ActiveInteractable active, int offer)
    {
        if (active.Uses[offer] > 0)
        {
            active.Uses[offer] -= 1;
        }

        List<InteractionOffer> offers = active.Definition.Offers;
        bool anyLeft = false;
        for (int index = 0; index < offers.Count; index++)
        {
            anyLeft |= offers[index].IsValid && active.Uses[index] != 0;
        }

        if (!anyLeft)
        {
            active.State = InteractableState.Completed;
            return;
        }

        float cooldown = active.Definition.Cooldown;
        if (cooldown > 0)
        {
            active.State = InteractableState.Cooldown;
            active.Cooldown = cooldown;
        }
    }

    private bool CanExecute(Entity interactor, Entity target, in ActiveInteractable active, int index)
    {
        if (active.Uses[index] == 0)
        {
            return false;
        }

        InteractionOffer offer = active.Definition.Offers[index];
        ConditionContext context = CreateContext(interactor, target);
        return offer.Conditions.Evaluate(context);
    }

    private bool IsInReach(Entity interactor, Entity target)
    {
        if (!World.TryGet(interactor, out Transform from) || !World.Has<Transform>(target))
        {
            return true;
        }

        float reach = World.Get<Interactor>(interactor).Reach;
        Vector3 point = GetUsePoint(World, target);
        float distance = Vector3.Distance(from.Position, point);
        return distance <= reach;
    }

    /// <summary>Where the entity is used, in the world: its <see cref="UsePoint"/> when it has one, else where it stands.</summary>
    public static Vector3 GetUsePoint(World world, Entity target)
    {
        Transform transform = world.Get<Transform>(target);
        if (!world.TryGet(target, out UsePoint point))
        {
            return transform.Position;
        }

        Matrix4x4 toWorld = transform.ToMatrix();
        return Vector3.Transform(point.Offset, toWorld);
    }

    private bool IsInteractable(Entity entity) => entity != Entity.Null && World.IsAlive(entity) && World.Has<ActiveInteractable>(entity);

    private bool TryGetFocus(Entity interactor, out Entity target)
    {
        target = Entity.Null;
        if (!World.IsAlive(interactor) || !World.TryGet(interactor, out InteractorFocus focus) || !IsInteractable(focus.Target))
        {
            return false;
        }

        target = focus.Target;
        return true;
    }

    private ConditionContext CreateContext(Entity interactor, Entity target)
    {
        WorldEntityView instigator = new(World, interactor);
        WorldEntityView acted = new(World, target);
        return new ConditionContext(_facts) { Instigator = instigator, Target = acted, Services = _services };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The interactable {Definition} has no data assets to load its definition from; it stays inactive.")]
    private partial void LogNoDataAssets(string definition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The interactable definition {Definition} can't be read, so it stays inactive: {Reason}")]
    private partial void LogDefinitionFailed(string definition, string reason);
}
