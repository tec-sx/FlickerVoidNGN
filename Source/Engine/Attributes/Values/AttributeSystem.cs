using Arch.Core;

using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>
/// Holds entities' attributes while the game plays, in place of Gameplay Ability System attribute sets: base values, modifiers that
/// add, multiply or override the current value and grant tags, and each value clamped by its <see cref="AttributeDefinition"/>.
/// Changes of current values are sent as <see cref="AttributeChangedEvent"/>s.
/// </summary>
public sealed class AttributeSystem
{
    private readonly World _world;
    private readonly EventBus _events;
    private readonly IServiceRegistry _services;

    public AttributeSystem(Game game)
    {
        _world = game.World;
        _events = game.Events;
        _services = game.Services;
        ArrayRegistry.Add<ActiveAttributeSet>();
        ArrayRegistry.Add<OwnedTags>();
        game.Lifecycle.OnActivate((Entity entity, ref AttributeSet set) => Activate(entity, set));
        game.Lifecycle.OnDeactivate((Entity entity, ref AttributeSet _) => Deactivate(entity));
    }

    /// <summary>The definition whose id is <paramref name="attribute"/>; null when no data asset describes it.</summary>
    public AttributeDefinition? FindDefinition(Tag attribute)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        return data?.Find<AttributeDefinition>(attribute);
    }

    public bool HasAttribute(Entity owner, Tag attribute) => FindValue(owner, attribute) is not null;

    /// <summary>The current value, with modifiers; 0 when the entity lacks the attribute.</summary>
    public float GetValue(Entity owner, Tag attribute) => FindValue(owner, attribute)?.Current ?? 0;

    /// <summary>The base value, without modifiers; 0 when the entity lacks the attribute.</summary>
    public float GetBaseValue(Entity owner, Tag attribute) => FindValue(owner, attribute)?.Base ?? 0;

    /// <summary>The attributes the entity has, by attribute; empty when it has none.</summary>
    public IReadOnlyDictionary<Tag, AttributeValue> GetValues(Entity owner)
    {
        if (!TryGetSet(owner, out ActiveAttributeSet set))
        {
            return new Dictionary<Tag, AttributeValue>();
        }

        return set.Values;
    }

    /// <summary>Permanently sets the base value, clamped; false when the entity lacks the attribute.</summary>
    public bool SetBaseValue(Entity owner, Tag attribute, float value)
    {
        AttributeValue? current = FindValue(owner, attribute);
        if (current is null)
        {
            return false;
        }

        current.Base = current.Clamp(value);
        Recalculate(owner, attribute);
        return true;
    }

    /// <summary>Permanently adds <paramref name="delta"/> to the base value; false when the entity lacks the attribute.</summary>
    public bool AddToBaseValue(Entity owner, Tag attribute, float delta)
    {
        AttributeValue? current = FindValue(owner, attribute);
        if (current is null)
        {
            return false;
        }

        return SetBaseValue(owner, attribute, current.Base + delta);
    }

    public IReadOnlyList<AppliedModifier> GetModifiers(Entity owner)
    {
        if (!TryGetSet(owner, out ActiveAttributeSet set))
        {
            return [];
        }

        return set.Modifiers;
    }

    /// <summary>Puts the modifier on the entity until it is removed; returns its handle, or an empty one when the entity has no attributes.</summary>
    public Guid ApplyModifier(Entity owner, ModifierDefinition modifier)
    {
        if (!TryGetSet(owner, out ActiveAttributeSet set))
        {
            return Guid.Empty;
        }

        Guid handle = Guid.NewGuid();
        set.Modifiers.Add(new AppliedModifier(handle, modifier));
        Refresh(owner, modifier);
        return handle;
    }

    public bool RemoveModifier(Entity owner, Guid handle)
    {
        if (!TryGetSet(owner, out ActiveAttributeSet set))
        {
            return false;
        }

        int index = set.Modifiers.FindIndex(applied => applied.Handle == handle);
        if (index < 0)
        {
            return false;
        }

        ModifierDefinition modifier = set.Modifiers[index].Modifier;
        set.Modifiers.RemoveAt(index);
        Refresh(owner, modifier);
        return true;
    }

    /// <summary>Takes off every instance of the modifier, such as a status a quest step ends; returns how many came off.</summary>
    public int RemoveModifiers(Entity owner, ModifierDefinition modifier)
    {
        if (!TryGetSet(owner, out ActiveAttributeSet set))
        {
            return 0;
        }

        int removed = set.Modifiers.RemoveAll(applied => applied.Modifier == modifier);
        if (removed > 0)
        {
            Refresh(owner, modifier);
        }

        return removed;
    }

    private void Activate(Entity entity, AttributeSet set)
    {
        Dictionary<Tag, AttributeValue> values = [];
        foreach (AttributeEntry entry in set.Values)
        {
            AttributeDefinition? definition = FindDefinition(entry.Attribute);
            AttributeValue value = new(definition, entry.Value);
            value.Base = value.Clamp(entry.Value);
            value.Current = value.Base;
            values[entry.Attribute] = value;
        }

        ActiveAttributeSet active = new() { Values = values, Modifiers = [], AppliedTags = [] };
        _world.Add(entity, active);
    }

    private void Deactivate(Entity entity)
    {
        if (!_world.TryGet(entity, out ActiveAttributeSet set))
        {
            return;
        }

        GrantedTags.Replace(_world, entity, set.AppliedTags, []);
        _world.Remove<ActiveAttributeSet>(entity);
    }

    /// <summary>Recalculates what the modifier changes and the granted tags after it went on or came off.</summary>
    private void Refresh(Entity owner, ModifierDefinition modifier)
    {
        HashSet<Tag> changed = [];
        foreach (AttributeModifier change in modifier.Changes)
        {
            if (changed.Add(change.Attribute))
            {
                Recalculate(owner, change.Attribute);
            }
        }

        ref ActiveAttributeSet set = ref _world.Get<ActiveAttributeSet>(owner);
        List<Tag> granted = [];
        foreach (AppliedModifier applied in set.Modifiers)
        {
            granted.AddRange(applied.Modifier.GrantedTags);
        }

        List<Tag> previous = set.AppliedTags;
        set.AppliedTags = granted;
        GrantedTags.Replace(_world, owner, previous, granted);
    }

    private void Recalculate(Entity owner, Tag attribute)
    {
        ActiveAttributeSet set = _world.Get<ActiveAttributeSet>(owner);
        if (!set.Values.TryGetValue(attribute, out AttributeValue? value))
        {
            return;
        }

        float added = 0;
        float multiplier = 1;
        float? overridden = null;
        foreach (AppliedModifier applied in set.Modifiers)
        {
            foreach (AttributeModifier change in applied.Modifier.Changes)
            {
                if (change.Attribute != attribute)
                {
                    continue;
                }

                switch (change.Operation)
                {
                    case ModifierOperation.Add:
                        added += change.Value;
                        break;
                    case ModifierOperation.Multiply:
                        multiplier *= change.Value;
                        break;
                    case ModifierOperation.Override:
                        overridden = change.Value;
                        break;
                }
            }
        }

        float raw = overridden ?? ((value.Base + added) * multiplier);
        float current = value.Clamp(raw);
        float old = value.Current;
        value.Current = current;
        if (old != current)
        {
            _events.Send(new AttributeChangedEvent(owner, attribute, old, current));
        }
    }

    private AttributeValue? FindValue(Entity owner, Tag attribute)
    {
        if (!TryGetSet(owner, out ActiveAttributeSet set))
        {
            return null;
        }

        set.Values.TryGetValue(attribute, out AttributeValue? value);
        return value;
    }

    private bool TryGetSet(Entity owner, out ActiveAttributeSet set)
    {
        if (!_world.IsAlive(owner))
        {
            set = default;
            return false;
        }

        return _world.TryGet(owner, out set);
    }
}
