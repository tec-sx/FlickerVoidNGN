namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

/// <summary>The player's titles: earned ones are kept for good, and the one that leads is current.</summary>
public interface ITitles
{
    /// <summary>A title was earned for the first time.</summary>
    event Action<TitleDefinition>? TitleEarned;

    /// <summary>A different title became current, newly earned or one held before.</summary>
    event Action<TitleDefinition>? CurrentTitleChanged;

    /// <summary>The title shown as the player's current one; null when none is held.</summary>
    TitleDefinition? CurrentTitle { get; }

    /// <summary>Held titles, current first, then from most to least recently current, then those never current.</summary>
    List<TitleDefinition> GetHeldTitles();

    /// <summary>Re-checks every title. Call it when a non-fact value a title checks changes.</summary>
    void EvaluateTitles();

    /// <summary>Makes the title current, earning it first (with its OnEarned effects) if the player doesn't hold it yet.</summary>
    void PromoteTitle(TitleDefinition title);
}
