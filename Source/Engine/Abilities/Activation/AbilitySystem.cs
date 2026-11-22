using Arch.Core;

using FlickerVoid.Engine.Abilities.Costs;
using FlickerVoid.Engine.Abilities.Definitions;
using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>
/// Runs entities' abilities while the game plays, in place of the Gameplay Ability System: grants the abilities of an entity's sets,
/// activates them by input, rule or system when their tags, group, conditions and costs allow, cancels and blocks others by tag
/// (their own and the entity's tag relationships), adds their owned tags while they run, and sends what happens as events.
/// </summary>
public sealed partial class AbilitySystem
{
    private readonly World _world;
    private readonly EventBus _events;
    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly ILogger<AbilitySystem> _logger;

    public AbilitySystem(Game game)
    {
        _world = game.World;
        _events = game.Events;
        _facts = game.Facts;
        _services = game.Services;
        _logger = game.LoggerFactory.CreateLogger<AbilitySystem>();
        ArrayRegistry.Add<ActiveAbilities>();
        ArrayRegistry.Add<OwnedTags>();
        game.Lifecycle.OnActivate((Entity entity, ref GrantedAbilities granted) => Activate(entity, granted));
        game.Lifecycle.OnDeactivate((Entity entity, ref GrantedAbilities _) => Deactivate(entity));
    }

    /// <summary>The abilities the entity was granted, in the order its sets list them; empty when it has none.</summary>
    public IReadOnlyList<AbilitySpec> GetAbilities(Entity owner)
    {
        if (!TryGetAbilities(owner, out ActiveAbilities abilities))
        {
            return [];
        }

        return abilities.Specs;
    }

    /// <summary>The granted ability whose id is <paramref name="ability"/>, or else the first with that tag.</summary>
    public AbilitySpec? Find(Entity owner, Tag ability)
    {
        IReadOnlyList<AbilitySpec> specs = GetAbilities(owner);
        AbilitySpec? byId = specs.FirstOrDefault(spec => spec.Ability.Id == ability);
        return byId ?? specs.FirstOrDefault(spec => spec.Ability.Tags.Contains(ability));
    }

    public bool IsActive(Entity owner, Tag ability) => Find(owner, ability)?.IsActive ?? false;

    /// <summary>Grants one more ability, activated by <paramref name="input"/>; false when the entity has no abilities while playing.</summary>
    public bool Grant(Entity owner, AbilityDefinition ability, Tag input)
    {
        if (!TryGetAbilities(owner, out ActiveAbilities abilities))
        {
            return false;
        }

        AbilitySpec spec = new(ability, input);
        abilities.Specs.Add(spec);
        if (ability.Policy == AbilityPolicy.OnGranted)
        {
            TryActivate(owner, spec, Entity.Null);
        }

        return true;
    }

    /// <summary>Whether the ability could activate now, and if not, the <see cref="AbilityFailure"/> reason.</summary>
    public bool CanActivate(Entity owner, Tag ability, out Tag reason, Entity target = default)
    {
        AbilitySpec? spec = Find(owner, ability);
        if (spec is null)
        {
            reason = AbilityFailure.Missing;
            return false;
        }

        return CanActivate(owner, spec, target, out reason);
    }

    /// <summary>Activates the ability on <paramref name="target"/> when it can, else sends <see cref="AbilityFailedEvent"/>.</summary>
    public bool TryActivate(Entity owner, Tag ability, Entity target = default)
    {
        AbilitySpec? spec = Find(owner, ability);
        if (spec is null)
        {
            LogMissing(ability.Name);
            return false;
        }

        return TryActivate(owner, spec, target);
    }

    /// <summary>Activates the abilities bound to the input that activate on input.</summary>
    public void InputPressed(Entity owner, Tag input)
    {
        foreach (AbilitySpec spec in GetBound(owner, input))
        {
            if (!spec.IsActive && spec.Ability.Policy != AbilityPolicy.OnGranted)
            {
                TryActivate(owner, spec, Entity.Null);
            }
        }
    }

    /// <summary>Cancels the running abilities bound to the input that run while it is held.</summary>
    public void InputReleased(Entity owner, Tag input)
    {
        foreach (AbilitySpec spec in GetBound(owner, input))
        {
            if (spec.IsActive && spec.Ability.Policy == AbilityPolicy.WhileInputHeld)
            {
                Stop(owner, spec, cancelled: true);
            }
        }
    }

    /// <summary>Ends the running ability as finished, as its behaviour does when done.</summary>
    public bool End(Entity owner, Tag ability) => Stop(owner, ability, cancelled: false);

    /// <summary>Cuts the running ability short.</summary>
    public bool Cancel(Entity owner, Tag ability) => Stop(owner, ability, cancelled: true);

    private bool Stop(Entity owner, Tag ability, bool cancelled)
    {
        AbilitySpec? spec = Find(owner, ability);
        if (spec is null || !spec.IsActive)
        {
            return false;
        }

        Stop(owner, spec, cancelled);
        return true;
    }

    private bool TryActivate(Entity owner, AbilitySpec spec, Entity target)
    {
        if (!CanActivate(owner, spec, target, out Tag reason))
        {
            string message = GetFailureMessage(spec.Ability, reason);
            _events.Send(new AbilityFailedEvent(owner, spec.Ability, reason, message));
            return false;
        }

        Start(owner, spec, target);
        return true;
    }

    private bool CanActivate(Entity owner, AbilitySpec spec, Entity target, out Tag reason)
    {
        ActiveAbilities abilities = _world.Get<ActiveAbilities>(owner);
        AbilityDefinition ability = spec.Ability;
        AbilityTagRelationship[] relationships = GetRelationships(abilities, ability);
        reason = Tag.None;
        if (spec.IsActive)
        {
            reason = AbilityFailure.Active;
        }
        else if (IsBlockedByRunning(abilities, ability))
        {
            reason = AbilityFailure.Blocked;
        }
        else if (!HasAll(owner, ability.RequiredTags.Concat(relationships.SelectMany(relationship => relationship.RequiredTags))))
        {
            reason = AbilityFailure.TagsMissing;
        }
        else if (HasAny(owner, ability.BlockedTags.Concat(relationships.SelectMany(relationship => relationship.BlockedTags))))
        {
            reason = AbilityFailure.TagsBlocked;
        }
        else if (ability.Group != AbilityGroup.Independent && abilities.Specs.Any(other => other.IsActive && other.Ability.Group == AbilityGroup.ExclusiveBlock))
        {
            reason = AbilityFailure.Group;
        }
        else
        {
            ConditionContext context = CreateContext(owner, target);
            if (!ability.Conditions.IsEmpty && !ability.Conditions.Evaluate(context))
            {
                reason = AbilityFailure.Conditions;
            }
            else if (!ability.Costs.All(cost => cost.CanPay(context)))
            {
                reason = AbilityFailure.Cost;
            }
        }

        return !reason.IsValid;
    }

    private void Start(Entity owner, AbilitySpec spec, Entity target)
    {
        ActiveAbilities abilities = _world.Get<ActiveAbilities>(owner);
        AbilityDefinition ability = spec.Ability;
        AbilityTagRelationship[] relationships = GetRelationships(abilities, ability);
        List<Tag> cancels = [.. ability.CancelAbilitiesWithTags, .. relationships.SelectMany(relationship => relationship.CancelAbilitiesWithTags)];
        foreach (AbilitySpec other in abilities.Specs)
        {
            bool cancelledByTag = other.Ability.HasAnyTag(cancels);
            bool replaced = ability.Group != AbilityGroup.Independent && other.Ability.Group == AbilityGroup.ExclusiveReplace;
            if (other != spec && other.IsActive && (cancelledByTag || replaced))
            {
                Stop(owner, other, cancelled: true);
            }
        }

        spec.IsActive = true;
        spec.Target = target;
        ConditionContext context = CreateContext(owner, target);
        foreach (AbilityCost cost in ability.Costs)
        {
            cost.Pay(context);
        }

        RefreshTags(owner);
        _events.Send(new AbilityActivatedEvent(owner, ability, target));
        ability.OnActivated.Apply(context);
        if (ability.Instant && spec.IsActive)
        {
            Stop(owner, spec, cancelled: false);
        }
    }

    private void Stop(Entity owner, AbilitySpec spec, bool cancelled)
    {
        Entity target = spec.Target;
        spec.IsActive = false;
        spec.Target = Entity.Null;
        RefreshTags(owner);
        _events.Send(new AbilityEndedEvent(owner, spec.Ability, target, cancelled));
    }

    private void Activate(Entity entity, GrantedAbilities granted)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        AttributeSystem? attributes = _services.GetService<AttributeSystem>();
        List<AbilitySpec> specs = [];
        List<Guid> modifiers = [];
        AbilityTagRelationshipsDefinition? relationships = null;
        if (data is not null)
        {
            relationships = Load<AbilityTagRelationshipsDefinition>(data, granted.Relationships);
            foreach (string path in granted.Sets)
            {
                AbilitySetDefinition? set = Load<AbilitySetDefinition>(data, path);
                if (set is null)
                {
                    continue;
                }

                foreach (AbilitySetEntry entry in set.Abilities)
                {
                    AbilityDefinition? ability = Load<AbilityDefinition>(data, entry.Ability);
                    if (ability is not null)
                    {
                        specs.Add(new AbilitySpec(ability, entry.Input));
                    }
                }

                foreach (string modifierPath in set.Modifiers)
                {
                    ModifierDefinition? modifier = Load<ModifierDefinition>(data, modifierPath);
                    if (modifier is not null && attributes is not null)
                    {
                        Guid handle = attributes.ApplyModifier(entity, modifier);
                        modifiers.Add(handle);
                    }
                }
            }
        }

        ActiveAbilities active = new() { Specs = specs, Relationships = relationships, Modifiers = modifiers, AppliedTags = [] };
        _world.Add(entity, active);
        foreach (AbilitySpec spec in specs)
        {
            if (spec.Ability.Policy == AbilityPolicy.OnGranted)
            {
                TryActivate(entity, spec, Entity.Null);
            }
        }
    }

    private void Deactivate(Entity entity)
    {
        if (!_world.TryGet(entity, out ActiveAbilities abilities))
        {
            return;
        }

        foreach (AbilitySpec spec in abilities.Specs)
        {
            if (spec.IsActive)
            {
                Stop(entity, spec, cancelled: true);
            }
        }

        AttributeSystem? attributes = _services.GetService<AttributeSystem>();
        foreach (Guid handle in abilities.Modifiers)
        {
            attributes?.RemoveModifier(entity, handle);
        }

        _world.Remove<ActiveAbilities>(entity);
    }

    private void RefreshTags(Entity owner)
    {
        ref ActiveAbilities abilities = ref _world.Get<ActiveAbilities>(owner);
        List<Tag> granted = [];
        foreach (AbilitySpec spec in abilities.Specs)
        {
            if (spec.IsActive)
            {
                granted.AddRange(spec.Ability.OwnedTagsWhileActive);
            }
        }

        List<Tag> previous = abilities.AppliedTags;
        abilities.AppliedTags = granted;
        GrantedTags.Replace(_world, owner, previous, granted);
    }

    private static bool IsBlockedByRunning(ActiveAbilities abilities, AbilityDefinition ability)
    {
        foreach (AbilitySpec running in abilities.Specs)
        {
            if (!running.IsActive)
            {
                continue;
            }

            AbilityTagRelationship[] relationships = GetRelationships(abilities, running.Ability);
            IEnumerable<Tag> blocks = running.Ability.BlockAbilitiesWithTags.Concat(relationships.SelectMany(relationship => relationship.BlockAbilitiesWithTags));
            if (ability.HasAnyTag(blocks))
            {
                return true;
            }
        }

        return false;
    }

    private static AbilityTagRelationship[] GetRelationships(ActiveAbilities abilities, AbilityDefinition ability)
    {
        if (abilities.Relationships is null)
        {
            return [];
        }

        return abilities.Relationships.For(ability).ToArray();
    }

    private bool HasAll(Entity owner, IEnumerable<Tag> tags)
    {
        _world.TryGet(owner, out OwnedTags owned);
        return tags.All(tag => owned.Tags is not null && owned.HasMatchingTag(tag));
    }

    private bool HasAny(Entity owner, IEnumerable<Tag> tags)
    {
        if (!_world.TryGet(owner, out OwnedTags owned))
        {
            return false;
        }

        return tags.Any(owned.HasMatchingTag);
    }

    private IEnumerable<AbilitySpec> GetBound(Entity owner, Tag input)
    {
        if (!input.IsValid)
        {
            return [];
        }

        IReadOnlyList<AbilitySpec> specs = GetAbilities(owner);
        return specs.Where(spec => spec.Input == input).ToList();
    }

    private ConditionContext CreateContext(Entity owner, Entity target)
    {
        WorldEntityView instigator = new(_world, owner);
        WorldEntityView? targetView = null;
        if (target != Entity.Null && _world.IsAlive(target))
        {
            targetView = new WorldEntityView(_world, target);
        }

        return new ConditionContext(_facts) { Instigator = instigator, Target = targetView, Services = _services };
    }

    private static string GetFailureMessage(AbilityDefinition ability, Tag reason)
    {
        AbilityFailureMessage? message = ability.FailureMessages.FirstOrDefault(failure => reason.MatchesTag(failure.Reason));
        return message?.Text ?? string.Empty;
    }

    private T? Load<T>(DataAssets data, string? path)
        where T : class
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return data.Load<T>(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogUnreadable(path, exception.Message);
            return null;
        }
    }

    private bool TryGetAbilities(Entity owner, out ActiveAbilities abilities)
    {
        if (!_world.IsAlive(owner))
        {
            abilities = default;
            return false;
        }

        return _world.TryGet(owner, out abilities);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No granted ability is {Ability}.")]
    private partial void LogMissing(string ability);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Path} can't be read as an ability asset: {Reason}")]
    private partial void LogUnreadable(string path, string reason);
}
