using System.Numerics;

namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>Evaluates, scores and displays <see cref="Compare"/> operators.</summary>
public static class CompareExtensions
{
    /// <summary>Tolerance for float equality, matching Unreal's IsNearlyEqual default.</summary>
    public const float FloatTolerance = 1e-8f;

    public static bool Test<T>(this Compare compare, T current, T value)
        where T : IComparisonOperators<T, T, bool>
    {
        switch (compare)
        {
            case Compare.Equal:
                return current == value;
            case Compare.NotEqual:
                return current != value;
            case Compare.Greater:
                return current > value;
            case Compare.GreaterOrEqual:
                return current >= value;
            case Compare.Less:
                return current < value;
            case Compare.LessOrEqual:
                return current <= value;
            default:
                return false;
        }
    }

    /// <summary>Equal and NotEqual allow for float error.</summary>
    public static bool Test(this Compare compare, float current, float value)
    {
        switch (compare)
        {
            case Compare.Equal:
                return IsNearlyEqual(current, value);
            case Compare.NotEqual:
                return !IsNearlyEqual(current, value);
            default:
                return compare.Test<float>(current, value);
        }
    }

    /// <summary>Scores an at-least comparison (Greater or GreaterOrEqual); false for the others.</summary>
    public static bool TryScore(this Compare compare, float current, float value, out ConditionScore score)
    {
        if (compare != Compare.Greater && compare != Compare.GreaterOrEqual)
        {
            score = default;
            return false;
        }

        score = ConditionScore.AtLeast(current, value);
        return true;
    }

    public static string ToSymbol(this Compare compare)
    {
        switch (compare)
        {
            case Compare.Equal:
                return "==";
            case Compare.NotEqual:
                return "!=";
            case Compare.Greater:
                return ">";
            case Compare.GreaterOrEqual:
                return ">=";
            case Compare.Less:
                return "<";
            case Compare.LessOrEqual:
                return "<=";
            default:
                return "?";
        }
    }

    private static bool IsNearlyEqual(float a, float b) => MathF.Abs(a - b) <= FloatTolerance;
}
