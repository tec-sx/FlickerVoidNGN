using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>The saved state of the world as integers named by tags: standing, fame, quest state, knowledge.</summary>
public interface IFactDatabase
{
    /// <summary>Raised for each change of one fact.</summary>
    event FactChangedHandler? FactChanged;

    /// <summary>Raised after any change, including a reset or a restore.</summary>
    event Action? FactsChanged;

    FactSettings Settings { get; }

    Tag LastChangedTag { get; }

    IReadOnlyDictionary<Tag, int> AllFacts { get; }

    /// <summary>Replaces all facts with the defaults declared in the settings.</summary>
    void ResetToDefaults();

    /// <summary>The value, or 0 when the fact is undefined.</summary>
    int GetFact(Tag tag);

    bool IsFactDefined(Tag tag);

    /// <summary>Label of the current value for enum-like facts, or null.</summary>
    string? GetFactValueName(Tag tag);

    void SetFact(Tag tag, int value);

    void AddFact(Tag tag, int delta = 1);

    bool RemoveFact(Tag tag);

    /// <summary>Removes the facts at or below the parent tag and returns how many were removed.</summary>
    int RemoveFactsUnder(Tag parent);

    void ClearFacts();

    /// <summary>Replaces all facts with saved ones, without per-fact notifications.</summary>
    void RestoreFacts(IReadOnlyDictionary<Tag, int> facts);
}
