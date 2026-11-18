using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Scales;

/// <summary>A value kept in the fact named by Id, read through bands and changed through Modify, which passes the change on through links. Range and default come from the fact's definition.</summary>
public sealed class ScaleDefinition : Definition
{
    /// <summary>At most three, by the rule of three.</summary>
    public List<ScaleBand> Bands { get; init; } = [];

    /// <summary>Scales this one's applied changes feed, once each: a change passed on doesn't travel further. At most three.</summary>
    public List<ScaleLink> Links { get; init; } = [];

    public int GetValue(IFactDatabase facts) => facts.GetFact(Id);

    /// <summary>The band the current value falls in; null below every band.</summary>
    public ScaleBand? GetBand(IFactDatabase facts)
    {
        int value = GetValue(facts);
        return FindBand(value);
    }

    /// <summary>The band with the highest threshold the value reaches; null below every band.</summary>
    public ScaleBand? FindBand(int value)
    {
        ScaleBand? result = null;
        foreach (ScaleBand band in Bands)
        {
            if (value >= band.Threshold && (result is null || band.Threshold > result.Threshold))
            {
                result = band;
            }
        }

        return result;
    }

    /// <summary>Min and Max of the scale's fact; false when the fact has no range.</summary>
    public bool TryGetRange(FactSettings settings, out int min, out int max)
    {
        FactDefinition? fact = settings.FindDefinition(Id);
        if (fact is not { Clamp: true })
        {
            min = 0;
            max = 0;
            return false;
        }

        min = fact.Min;
        max = fact.Max;
        return true;
    }

    /// <summary>Adds delta within the scale's range and passes the change that applied on through the links, once. Returns the change that applied.</summary>
    public int Modify(IFactDatabase facts, int delta)
    {
        if (delta == 0)
        {
            return 0;
        }

        // Only the change that applied is passed on, so a reward at the edge of the range earns nothing more elsewhere.
        int applied = AddClamped(facts, delta);
        if (applied == 0)
        {
            return 0;
        }

        foreach (ScaleLink link in Links)
        {
            if (link.Scale == this)
            {
                continue;
            }

            int change = link.Magnitude ? Math.Abs(applied) : applied;
            int passed = RoundToInt(change * link.Factor);
            link.Scale.AddClamped(facts, passed);
        }

        return applied;
    }

    /// <summary>Rounds half up, as Unreal does, so ports give the same values.</summary>
    private static int RoundToInt(float value) => (int)MathF.Floor(value + 0.5f);

    /// <summary>Returns the change that applied after the fact's range clamped it.</summary>
    private int AddClamped(IFactDatabase facts, int delta)
    {
        if (!Id.IsValid || delta == 0)
        {
            return 0;
        }

        int before = facts.GetFact(Id);
        long wanted = (long)before + delta;
        int value = (int)Math.Clamp(wanted, int.MinValue, int.MaxValue);
        facts.SetFact(Id, value);

        int after = facts.GetFact(Id);
        return after - before;
    }
}
