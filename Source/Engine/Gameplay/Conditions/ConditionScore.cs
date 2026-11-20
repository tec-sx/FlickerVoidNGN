namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>How hard an at-least condition is to meet and how far past its threshold the value is. A higher threshold ranks first.</summary>
public readonly record struct ConditionScore(float Threshold, float Excess) : IComparable<ConditionScore>
{
    public static ConditionScore AtLeast(float current, float threshold) => new(threshold, current - threshold);

    public int CompareTo(ConditionScore other)
    {
        int byThreshold = Threshold.CompareTo(other.Threshold);
        if (byThreshold != 0)
        {
            return byThreshold;
        }

        return Excess.CompareTo(other.Excess);
    }

    public static bool operator <(ConditionScore left, ConditionScore right) => left.CompareTo(right) < 0;

    public static bool operator >(ConditionScore left, ConditionScore right) => left.CompareTo(right) > 0;

    public static bool operator <=(ConditionScore left, ConditionScore right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ConditionScore left, ConditionScore right) => left.CompareTo(right) >= 0;
}
