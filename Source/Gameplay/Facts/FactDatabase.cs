using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Facts kept in memory, defaulted and clamped by their definitions in the settings.</summary>
public sealed class FactDatabase : IFactDatabase
{
    private readonly Dictionary<Tag, int> _facts = [];

    public FactDatabase(FactSettings settings)
    {
        Settings = settings;
        ResetToDefaults();
    }

    public event FactChangedHandler? FactChanged;

    public event Action? FactsChanged;

    public FactSettings Settings { get; }

    public Tag LastChangedTag { get; private set; }

    public IReadOnlyDictionary<Tag, int> AllFacts => _facts;

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

    public int GetFact(Tag tag) => _facts.GetValueOrDefault(tag);

    public bool IsFactDefined(Tag tag) => _facts.ContainsKey(tag);

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
