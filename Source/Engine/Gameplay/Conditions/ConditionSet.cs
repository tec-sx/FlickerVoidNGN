namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>Conditions that must all hold, or any one of them, with how the player sees what they guard while they fail.</summary>
public sealed class ConditionSet
{
    public ConditionMode Mode { get; set; } = ConditionMode.All;

    public List<Condition> Conditions { get; set; } = [];

    public ConditionFailurePresentation FailurePresentation { get; set; } = ConditionFailurePresentation.Hidden;

    /// <summary>Shown with ShowLockedWithReason.</summary>
    public string FailureReason { get; set; } = string.Empty;

    public bool IsEmpty => Conditions.Count == 0;

    /// <summary>Indices of the sets that hold, hardest first: highest at-least threshold, then furthest past it. Unscored sets come last; ties keep the given order.</summary>
    public static int[] Rank(IReadOnlyList<ConditionSet> sets, ConditionContext context)
    {
        List<(int Index, bool Scored, ConditionScore Score)> held = [];
        for (int index = 0; index < sets.Count; index++)
        {
            ConditionSet set = sets[index];
            if (!set.Evaluate(context))
            {
                continue;
            }

            bool scored = set.TryGetScore(context, out ConditionScore score);
            held.Add((index, scored, score));
        }

        return held
            .OrderByDescending(ranked => ranked.Scored)
            .ThenByDescending(ranked => ranked.Score)
            .Select(ranked => ranked.Index)
            .ToArray();
    }

    public bool Evaluate(ConditionContext context)
    {
        bool all = Mode == ConditionMode.All;

        foreach (Condition condition in Conditions)
        {
            bool passed = condition.Check(context);
            if (all && !passed)
            {
                return false;
            }

            if (!all && passed)
            {
                return true;
            }
        }

        return all || IsEmpty;
    }

    /// <summary>Sum of the scored conditions (All, so more requirements rank higher) or the best one (Any); false when none is scored.</summary>
    public bool TryGetScore(ConditionContext context, out ConditionScore score)
    {
        bool all = Mode == ConditionMode.All;
        bool found = false;
        score = default;

        foreach (Condition condition in Conditions)
        {
            if (!condition.TryGetScore(context, out ConditionScore conditionScore))
            {
                continue;
            }

            if (!found)
            {
                score = conditionScore;
            }
            else if (all)
            {
                score = new ConditionScore(score.Threshold + conditionScore.Threshold, score.Excess + conditionScore.Excess);
            }
            else if (score < conditionScore)
            {
                score = conditionScore;
            }

            found = true;
        }

        return found;
    }

    public string Describe()
    {
        List<string> parts = [];
        foreach (Condition condition in Conditions)
        {
            string description = condition.Describe();
            if (condition.Invert)
            {
                description = $"NOT {description}";
            }

            parts.Add(description);
        }

        string separator = Mode == ConditionMode.All ? " AND " : " OR ";
        return string.Join(separator, parts);
    }
}
