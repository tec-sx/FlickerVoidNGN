using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Music;

public enum ArrangementKind
{
    Loop,
    FillIn,
    FillOut,

    /// <summary>The time a track is wanted but waits for its line of the grid.</summary>
    Wait,

    /// <summary>One sound thrown by a scatter track; has no length.</summary>
    Scatter,
}

/// <summary>A piece of a track's lane, in bars from the cue's start; for a loop, which repeat of it this is.</summary>
public readonly record struct ArrangementClip(ArrangementKind Kind, double Start, double Length, int Repeat);

/// <summary>A stretch of the rehearsal, in bars, during which a track is wanted.</summary>
public readonly record struct RehearsalSpan(double On, double Off);

/// <summary>The bars a track's loop and fills take on the grid; 0 for a fill the track has none of.</summary>
public readonly record struct TrackLengths(double LoopBars, double FillInBars, double FillOutBars);

/// <summary>
/// What the music system will play, laid out on the cue's grid for the editor's lanes: when a track is wanted (always, or in the
/// rehearsal's spans) the loop comes in on its line after the fill-in and goes out on its line with the fill-out, as
/// <see cref="MusicSystem"/> does with the same <see cref="MusicGrid"/>. A cue without a tempo has no lines and shows 10 seconds a bar.
/// </summary>
public static class MusicArrangement
{
    /// <summary>The seconds one bar of the lanes stands for in a cue without a tempo: an hour, as its ruler shows the day.</summary>
    public const double UntimedBarSeconds = 3600;

    /// <summary>The hours of the day a cue without a tempo spans.</summary>
    public const double DayHours = WorldClock.DayHours;

    public static double GetBarSeconds(MusicCue cue)
    {
        if (cue.Tempo <= 0)
        {
            return UntimedBarSeconds;
        }

        return 60 / cue.Tempo * cue.BeatsPerBar;
    }

    /// <summary>The track's lengths in bars from its files, rounded to whole bars, at least one; its own <c>bars</c> for the loop when it has them.</summary>
    public static TrackLengths Measure(MusicCue cue, MusicTrack track, ClipCatalog catalog)
    {
        double stretch = cue.Tempo > 0 && track.Tempo > 0 ? track.Tempo / cue.Tempo : 1;
        double loop = track.Bars;
        if (loop <= 0)
        {
            loop = GetBars(cue, catalog.Find(track.Loop), 4, stretch);
        }

        double fillIn = GetBars(cue, catalog.Find(track.FillIn), 0, stretch);
        double fillOut = GetBars(cue, catalog.Find(track.FillOut), 0, stretch);
        return new TrackLengths(loop, fillIn, fillOut);
    }

    public static IReadOnlyList<ArrangementClip> Plan(MusicCue cue, MusicTrack track, TrackLengths lengths, IReadOnlyList<RehearsalSpan> spans, double totalBars)
    {
        List<RehearsalSpan> wanted = [];
        bool hours = cue.Tempo <= 0 && !track.IsRequested;
        if (hours)
        {
            wanted.AddRange(GetHours(track.When).Select(span => new RehearsalSpan(span.On, Math.Min(span.Off, totalBars))).Where(span => span.On < span.Off));
        }
        else if (track.IsRequested)
        {
            wanted.AddRange(spans.OrderBy(span => span.On));
        }
        else
        {
            wanted.Add(new RehearsalSpan(0, totalBars));
        }

        if (track.Scatter.Length > 0)
        {
            return Scatter(cue, track, wanted, totalBars);
        }

        List<ArrangementClip> clips = [];
        bool timed = cue.Tempo > 0;
        if (hours)
        {
            foreach (RehearsalSpan span in wanted)
            {
                clips.Add(new ArrangementClip(ArrangementKind.Loop, span.On, span.Off - span.On, 0));
            }

            return clips;
        }

        double startUnit = timed ? MusicGrid.GetUnit(cue, track.Start) : 0;
        double stopUnit = timed ? MusicGrid.GetUnit(cue, track.Stop) : 0;
        double busyUntil = 0;
        foreach (RehearsalSpan span in wanted)
        {
            bool fresh = !track.IsRequested;
            double asked = Math.Max(span.On, busyUntil);
            double start = fresh ? span.On : MusicGrid.NextLine(asked, startUnit);
            if (start >= totalBars)
            {
                break;
            }

            if (start > asked)
            {
                if (start - asked > 1e-6)
                {
                    clips.Add(new ArrangementClip(ArrangementKind.Wait, asked, start - asked, 0));
                }
            }

            double loopStart = start;
            if (!fresh && lengths.FillInBars > 0)
            {
                clips.Add(new ArrangementClip(ArrangementKind.FillIn, start, lengths.FillInBars, 0));
                loopStart = start + lengths.FillInBars;
            }

            double stop = fresh ? span.Off : MusicGrid.NextLine(span.Off, stopUnit);
            if (stop <= loopStart)
            {
                busyUntil = loopStart;
                continue;
            }

            AddLoops(clips, lengths.LoopBars, loopStart, Math.Min(stop, totalBars));
            if (!fresh && lengths.FillOutBars > 0 && stop < totalBars)
            {
                clips.Add(new ArrangementClip(ArrangementKind.FillOut, stop, lengths.FillOutBars, 0));
            }

            busyUntil = stop;
        }

        return clips;
    }

    /// <summary>
    /// The hours of the day a track is wanted, read from the <c>Time.Hour</c> tests of its conditions (all of them, or any); a track
    /// with none of them is wanted all day.
    /// </summary>
    public static IReadOnlyList<RehearsalSpan> GetHours(ConditionSet when)
    {
        int hours = (int)DayHours;
        bool all = when.Mode == ConditionMode.All;
        bool[] held = new bool[hours];
        Array.Fill(held, all);
        bool read = false;
        foreach (FactCondition test in when.Conditions.OfType<FactCondition>().Where(test => test.Fact.ToString() == WorldClock.HourFact.ToString() && !test.Invert))
        {
            read = true;
            for (int hour = 0; hour < hours; hour++)
            {
                bool passes = test.Compare.Test(hour, test.Value);
                held[hour] = all ? held[hour] && passes : held[hour] || passes;
            }
        }

        if (!read)
        {
            Array.Fill(held, true);
        }

        List<RehearsalSpan> spans = [];
        for (int hour = 0; hour < hours; hour++)
        {
            if (!held[hour])
            {
                continue;
            }

            int end = hour;
            while (end < hours && held[end])
            {
                end++;
            }

            spans.Add(new RehearsalSpan(hour, end));
            hour = end;
        }

        return spans;
    }

    private static double GetBars(MusicCue cue, ClipInfo? info, double fallback, double stretch)
    {
        if (info is null)
        {
            return fallback;
        }

        double bars = Math.Round(info.Seconds * stretch / GetBarSeconds(cue));
        return Math.Max(1, bars);
    }

    private static void AddLoops(List<ArrangementClip> clips, double loopBars, double from, double to)
    {
        double length = Math.Max(0.25, loopBars);
        int repeat = 0;
        for (double at = from; at < to - 0.0001; at += length, repeat++)
        {
            double shown = Math.Min(length, to - at);
            clips.Add(new ArrangementClip(ArrangementKind.Loop, at, shown, repeat));
        }
    }

    /// <summary>Marks where a scatter track might throw its sounds: every mean wait, nudged by a fixed pattern so the marks don't stand in rows.</summary>
    private static List<ArrangementClip> Scatter(MusicCue cue, MusicTrack track, List<RehearsalSpan> wanted, double totalBars)
    {
        List<ArrangementClip> marks = [];
        double barSeconds = GetBarSeconds(cue);
        double mean = Math.Max(1, (track.EveryMin + track.EveryMax) / 2) / barSeconds;
        if (cue.Tempo <= 0)
        {
            mean = Math.Clamp(mean * 60 * 0.25, 0.5, 3);
        }
        foreach (RehearsalSpan span in wanted)
        {
            int index = 0;
            for (double at = span.On + (mean / 2); at < Math.Min(span.Off, totalBars); at += mean, index++)
            {
                double nudge = ((index * 7 % 5) - 2) * 0.1 * mean;
                double place = Math.Clamp(at + nudge, span.On, Math.Min(span.Off, totalBars));
                marks.Add(new ArrangementClip(ArrangementKind.Scatter, place, 0, index));
            }
        }

        return marks;
    }
}
