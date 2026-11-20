using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Evaluates, scores and displays <see cref="FactCompare"/> operators.</summary>
public static class FactCompareExtensions
{
    /// <summary>Compares a fact, including the existence checks.</summary>
    public static bool Test(this FactCompare compare, IFactDatabase facts, Tag fact, int value)
    {
        switch (compare)
        {
            case FactCompare.Defined:
                return facts.IsFactDefined(fact);
            case FactCompare.Undefined:
                return !facts.IsFactDefined(fact);
            default:
                int current = facts.GetFact(fact);
                return compare.Test(current, value);
        }
    }

    /// <summary>Compares a value read from anywhere; Defined and Undefined need a fact and return false.</summary>
    public static bool Test(this FactCompare compare, int current, int value)
    {
        return compare.TryToCompare(out Compare valueCompare) && valueCompare.Test(current, value);
    }

    /// <summary>Scores an at-least comparison; false for the others.</summary>
    public static bool TryScore(this FactCompare compare, int current, int value, out ConditionScore score)
    {
        if (!compare.TryToCompare(out Compare valueCompare))
        {
            score = default;
            return false;
        }

        return valueCompare.TryScore(current, value, out score);
    }

    /// <summary>The value comparison behind a fact comparison; false for Defined and Undefined.</summary>
    public static bool TryToCompare(this FactCompare compare, out Compare valueCompare)
    {
        switch (compare)
        {
            case FactCompare.Equal:
                valueCompare = Compare.Equal;
                return true;
            case FactCompare.NotEqual:
                valueCompare = Compare.NotEqual;
                return true;
            case FactCompare.Greater:
                valueCompare = Compare.Greater;
                return true;
            case FactCompare.GreaterOrEqual:
                valueCompare = Compare.GreaterOrEqual;
                return true;
            case FactCompare.Less:
                valueCompare = Compare.Less;
                return true;
            case FactCompare.LessOrEqual:
                valueCompare = Compare.LessOrEqual;
                return true;
            default:
                valueCompare = default;
                return false;
        }
    }

    public static string ToSymbol(this FactCompare compare)
    {
        switch (compare)
        {
            case FactCompare.Defined:
                return "defined";
            case FactCompare.Undefined:
                return "undefined";
            default:
                compare.TryToCompare(out Compare valueCompare);
                return valueCompare.ToSymbol();
        }
    }
}
