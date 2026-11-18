using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>The saved state of the world as integers named by tags: standing, fame, quest state, knowledge.</summary>
public sealed class FactDatabase
{
    private readonly Dictionary<Tag, int> _facts = [];

    public FactDatabase(FactSettings settings)
    {
        Settings = settings;
        ResetToDefaults();
    }

    /// <summary>Raised for each change of one fact.</summary>
    public event FactChangedHandler? FactChanged;

    /// <summary>Raised after any change, including a reset or a restore.</summary>
    public event Action? FactsChanged;

    public FactSettings Settings { get; }

    public Tag LastChangedTag { get; private set; }

    public IReadOnlyDictionary<Tag, int> AllFacts => _facts;

    /// <summary>Replaces all facts with the defaults declared in the settings.</summary>
    public void ResetToDefaults()
    {
        _facts.Clear();
        foreach (FactDefinition definition in Settings.Definitions)
        {
            if (!definition.Tag.IsValid)
            {
                continue;
            }

            int value = definition.DefaultValue;
            if (definition.Clamp)
            {
                value = Math.Clamp(value, definition.Min, definition.Max);
            }

            _facts[definition.Tag] = value;
        }

        LastChangedTag = Tag.None;
        FactsChanged?.Invoke();
    }

    /// <summary>The value, or 0 when the fact is undefined.</summary>
    public int GetFact(Tag tag) => _facts.GetValueOrDefault(tag);

    public bool IsFactDefined(Tag tag) => _facts.ContainsKey(tag);

    /// <summary>Label of the current value for enum-like facts, or null.</summary>
    public string? GetFactValueName(Tag tag)
    {
        int value = GetFact(tag);
        return Settings.GetValueName(tag, value);
    }

    public void SetFact(Tag tag, int value)
    {
        WriteFact(tag, value);
    }

    public void AddFact(Tag tag, int delta = 1)
    {
        int value = GetFact(tag);
        WriteFact(tag, value + delta);
    }

    public bool RemoveFact(Tag tag)
    {
        if (!_facts.Remove(tag, out int oldValue))
        {
            return false;
        }

        Notify(tag, oldValue, 0);
        return true;
    }

    /// <summary>Removes the facts at or below the parent tag and returns how many were removed.</summary>
    public int RemoveFactsUnder(Tag parent)
    {
        List<Tag> toRemove = [];
        foreach (Tag tag in _facts.Keys)
        {
            if (tag.MatchesTag(parent))
            {
                toRemove.Add(tag);
            }
        }

        foreach (Tag tag in toRemove)
        {
            RemoveFact(tag);
        }

        return toRemove.Count;
    }

    public void ClearFacts()
    {
        Tag[] tags = [.. _facts.Keys];
        foreach (Tag tag in tags)
        {
            RemoveFact(tag);
        }
    }

    /// <summary>Replaces all facts with saved ones, without per-fact notifications.</summary>
    public void RestoreFacts(IReadOnlyDictionary<Tag, int> facts)
    {
        _facts.Clear();
        foreach ((Tag tag, int value) in facts)
        {
            _facts[tag] = value;
        }

        LastChangedTag = Tag.None;
        FactsChanged?.Invoke();
    }

    private void WriteFact(Tag tag, int value)
    {
        if (!tag.IsValid)
        {
            throw new ArgumentException("A fact needs a valid tag.", nameof(tag));
        }

        FactDefinition? definition = Settings.FindDefinition(tag);
        if (definition is { Clamp: true })
        {
            value = Math.Clamp(value, definition.Min, definition.Max);
        }

        int oldValue = GetFact(tag);
        _facts[tag] = value;
        Notify(tag, oldValue, value);
    }

    private void Notify(Tag tag, int oldValue, int newValue)
    {
        LastChangedTag = tag;
        FactChanged?.Invoke(tag, oldValue, newValue);
        FactsChanged?.Invoke();
    }
}
