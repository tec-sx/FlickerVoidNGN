namespace FlickerVoid.Engine.Runtime.Curves;

/// <summary>Finds a time among keys and blends between them, one number at a time, for every kind of curve.</summary>
internal static class CurveMath
{
    private const float MinSpan = 1e-6f;

    /// <summary>Keeps a smooth slope from passing a neighbour's value (Fritsch and Carlson's bound for a monotone cubic).</summary>
    private const float SlopeLimit = 3;

    /// <summary>
    /// Where a time falls among at least one key's sorted times: clamped to the ends without a period, and with one,
    /// wrapped into it with the last key leading on to the first.
    /// </summary>
    public static CurvePlace Locate(ReadOnlySpan<float> times, float time, float period)
    {
        int last = times.Length - 1;
        if (last == 0)
        {
            return Hold(0, time);
        }

        if (period > 0)
        {
            return LocateWrapped(times, time, period);
        }

        if (time <= times[0])
        {
            return Hold(0, time);
        }

        if (time >= times[last])
        {
            return Hold(last, time);
        }

        int from = FindLastAtOrBefore(times, time);
        int after = from + 2;
        bool hasBefore = from > 0;
        bool hasAfter = after <= last;
        return new CurvePlace
        {
            Time = time,
            Before = hasBefore ? from - 1 : CurvePlace.None,
            BeforeTime = hasBefore ? times[from - 1] : 0,
            From = from,
            FromTime = times[from],
            To = from + 1,
            ToTime = times[from + 1],
            After = hasAfter ? after : CurvePlace.None,
            AfterTime = hasAfter ? times[after] : 0,
        };
    }

    /// <summary>The value at a place between two keys; the keys before and after shape the smooth slopes when the place has them.</summary>
    public static float Blend(in CurvePlace place, in CurveKey before, in CurveKey from, in CurveKey to, in CurveKey after)
    {
        float span = place.ToTime - place.FromTime;
        if (from.Mode == CurveMode.Constant || span < MinSpan)
        {
            return from.Value;
        }

        float along = (place.Time - place.FromTime) / span;
        if (from.Mode == CurveMode.Linear)
        {
            return float.Lerp(from.Value, to.Value, along);
        }

        float chord = (to.Value - from.Value) / span;
        float leave = from.Out;
        if (from.Mode == CurveMode.Smooth)
        {
            bool hasBefore = place.Before != CurvePlace.None;
            float into = Slope(place.BeforeTime, before.Value, place.FromTime, from.Value, hasBefore);
            leave = Smooth(into, chord, place.ToTime - place.BeforeTime, to.Value - before.Value);
        }

        float arrive = chord;
        if (to.Mode == CurveMode.Free)
        {
            arrive = to.In;
        }
        else if (to.Mode == CurveMode.Smooth)
        {
            bool hasAfter = place.After != CurvePlace.None;
            float outOf = Slope(place.ToTime, to.Value, place.AfterTime, after.Value, hasAfter);
            arrive = Smooth(chord, outOf, place.AfterTime - place.FromTime, after.Value - from.Value);
        }

        return Hermite(from.Value, leave * span, to.Value, arrive * span, along);
    }

    private static CurvePlace Hold(int index, float time) => new()
    {
        Time = time,
        Before = CurvePlace.None,
        From = index,
        To = CurvePlace.None,
        After = CurvePlace.None,
    };

    private static CurvePlace LocateWrapped(ReadOnlySpan<float> times, float time, float period)
    {
        int last = times.Length - 1;
        float turns = MathF.Floor(time / period);
        float wrapped = time - (turns * period);
        if (wrapped >= period)
        {
            wrapped = 0;
        }

        int from = last;
        float fromShift = -period;
        if (wrapped >= times[0])
        {
            from = FindLastAtOrBefore(times, wrapped);
            fromShift = 0;
        }

        int to = from == last ? 0 : from + 1;
        float toShift = from == last ? fromShift + period : 0;
        int before = from == 0 ? last : from - 1;
        float beforeShift = from == 0 ? -period : fromShift;
        int after = to == last ? 0 : to + 1;
        float afterShift = to == last ? toShift + period : toShift;
        return new CurvePlace
        {
            Time = wrapped,
            Before = before,
            BeforeTime = times[before] + beforeShift,
            From = from,
            FromTime = times[from] + fromShift,
            To = to,
            ToTime = times[to] + toShift,
            After = after,
            AfterTime = times[after] + afterShift,
        };
    }

    private static int FindLastAtOrBefore(ReadOnlySpan<float> times, float time)
    {
        int low = 0;
        int high = times.Length - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (times[middle] <= time)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    /// <summary>The slope between two keys, or none (NaN) when there is no neighbour or it shares the key's time.</summary>
    private static float Slope(float fromTime, float fromValue, float toTime, float toValue, bool exists)
    {
        float span = toTime - fromTime;
        if (!exists || span < MinSpan)
        {
            return float.NaN;
        }

        return (toValue - fromValue) / span;
    }

    /// <summary>
    /// The slope through a smooth key from the slopes into and out of it: the one it has at an end, flat at a peak or a dip,
    /// and otherwise the slope between its neighbours, held back so the curve never passes them.
    /// </summary>
    private static float Smooth(float into, float outOf, float neighbourSpan, float neighbourRise)
    {
        if (float.IsNaN(into))
        {
            return float.IsNaN(outOf) ? 0 : outOf;
        }

        if (float.IsNaN(outOf))
        {
            return into;
        }

        if (into * outOf <= 0)
        {
            return 0;
        }

        float through = neighbourRise / neighbourSpan;
        float intoSize = MathF.Abs(into);
        float outOfSize = MathF.Abs(outOf);
        float gentlest = MathF.Min(intoSize, outOfSize);
        float throughSize = MathF.Abs(through);
        float held = MathF.Min(throughSize, SlopeLimit * gentlest);
        return MathF.CopySign(held, through);
    }

    private static float Hermite(float from, float leave, float to, float arrive, float along)
    {
        float squared = along * along;
        float cubed = squared * along;
        float fromWeight = (2 * cubed) - (3 * squared) + 1;
        float leaveWeight = cubed - (2 * squared) + along;
        float toWeight = (3 * squared) - (2 * cubed);
        float arriveWeight = cubed - squared;
        return (from * fromWeight) + (leave * leaveWeight) + (to * toWeight) + (arrive * arriveWeight);
    }
}
