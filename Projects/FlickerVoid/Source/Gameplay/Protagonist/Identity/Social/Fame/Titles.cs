using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

/// <summary>
/// Keeps the player's titles: earned ones are never lost, and the hardest one to earn whose conditions hold leads (highest threshold, then furthest past it);
/// the leader becomes current whenever it changes. A title the story promotes stays current until the leader changes again.
/// </summary>
public sealed class Titles : IDisposable
{
    private const int MaxPasses = 8;

    private readonly IFactDatabase _facts;
    private readonly List<TitleDefinition> _titles;
    private TitleDefinition? _leadingTitle;
    private bool _evaluating;
    private bool _dirty;
    private bool _tracked;

    /// <summary>Tracks the settings' titles, re-checking them whenever a fact changes; the first check only records the leader, so a promoted title stays current after a load.</summary>
    public Titles(IFactDatabase facts, SocialSettings settings)
    {
        _facts = facts;
        _titles = settings.Titles;
        _facts.FactChanged += HandleFactChanged;
        EvaluateTitles();
    }

    /// <summary>A title was earned for the first time.</summary>
    public event Action<TitleDefinition>? TitleEarned;

    /// <summary>A different title became current, newly earned or one held before.</summary>
    public event Action<TitleDefinition>? CurrentTitleChanged;

    /// <summary>The title shown as the player's current one; null when none is held.</summary>
    public TitleDefinition? CurrentTitle
    {
        get
        {
            List<TitleDefinition> held = GetHeldTitles();
            return held.Count > 0 ? held[0] : null;
        }
    }

    /// <summary>Held titles, current first, then from most to least recently current, then those never current.</summary>
    public List<TitleDefinition> GetHeldTitles()
    {
        return _titles
            .Where(title => title.IsEarned(_facts))
            .OrderByDescending(title => title.GetRank(_facts))
            .ToList();
    }

    /// <summary>Re-checks every title; called on fact changes. Call it when a non-fact value a title checks changes.</summary>
    public void EvaluateTitles()
    {
        // Earning a title writes facts and runs its effects, which call back in here; those changes re-run the pass.
        if (_evaluating)
        {
            _dirty = true;
            return;
        }

        _evaluating = true;
        try
        {
            int pass = 0;
            do
            {
                _dirty = false;
                EvaluateTitlesOnce();
            }
            while (_dirty && ++pass < MaxPasses);
        }
        finally
        {
            _evaluating = false;
        }
    }

    /// <summary>Makes the title current, earning it first (with its OnEarned effects) if the player doesn't hold it yet.</summary>
    public void PromoteTitle(TitleDefinition title)
    {
        if (!title.Id.IsValid || CurrentTitle == title)
        {
            return;
        }

        bool firstTime = !title.IsEarned(_facts);
        MakeCurrent(title);

        if (firstTime)
        {
            ApplyEarned(title);
        }

        CurrentTitleChanged?.Invoke(title);
    }

    public void Dispose()
    {
        _facts.FactChanged -= HandleFactChanged;
    }

    private void HandleFactChanged(Tag tag, int oldValue, int newValue)
    {
        EvaluateTitles();
    }

    private void EvaluateTitlesOnce()
    {
        ConditionContext context = new(_facts);

        // Ranking keeps the given order on ties, so the more recently current title wins a tie.
        List<TitleDefinition> candidates = _titles
            .Where(static title => title.Id.IsValid)
            .OrderByDescending(title => title.GetRank(_facts))
            .ToList();
        List<ConditionSet> sets = candidates.ConvertAll(static title => title.Conditions);

        int[] ranked = ConditionSet.Rank(sets, context);
        TitleDefinition? best = ranked.Length > 0 ? candidates[ranked[0]] : null;

        List<TitleDefinition> newTitles = [];
        foreach (int index in ranked)
        {
            TitleDefinition title = candidates[index];
            if (!title.IsEarned(_facts))
            {
                newTitles.Add(title);
            }
        }

        // The first pass after a load only records the leader, so a title the story promoted stays current.
        if (best != _leadingTitle)
        {
            _leadingTitle = best;
            if (best is not null && (_tracked || newTitles.Count > 0))
            {
                PromoteTitle(best);
            }
        }

        foreach (TitleDefinition title in newTitles)
        {
            if (!title.IsEarned(_facts))
            {
                EarnTitle(title);
            }
        }

        _tracked = true;
    }

    private void EarnTitle(TitleDefinition title)
    {
        // Rank 1 places it below every title that has been current.
        _facts.SetFact(title.Id, 1);
        ApplyEarned(title);
    }

    private void ApplyEarned(TitleDefinition title)
    {
        ConditionContext context = new(_facts);
        title.OnEarned.Apply(context);
        TitleEarned?.Invoke(title);
    }

    private void MakeCurrent(TitleDefinition title)
    {
        int topRank = 0;
        foreach (TitleDefinition other in _titles)
        {
            int rank = other.GetRank(_facts);
            topRank = Math.Max(topRank, rank);
        }

        // Rank 1 is for titles earned but never current, so a current title ranks at least 2.
        int currentRank = Math.Max(topRank, 1) + 1;
        _facts.SetFact(title.Id, currentRank);
    }
}
