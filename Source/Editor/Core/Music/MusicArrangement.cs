using FlickerVoid.Engine.Audio.Music;

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
    /// <summary>The seconds one bar of the lanes stands for in a cue without a tempo.</summary>
    public const double UntimedBarSeconds = 10;

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
        double loop = track.Bars;
        if (loop <= 0)
        {
            loop = GetBars(cue, catalog.Find(track.Loop), 4);
        }

        double fillIn = GetBars(cue, catalog.Find(track.FillIn), 0);
        double fillOut = GetBars(cue, catalog.Find(track.FillOut), 0);
        return new TrackLengths(loop, fillIn, fillOut);
    }

    public static IReadOnlyList<ArrangementClip> Plan(MusicCue cue, MusicTrack track, TrackLengths lengths, IReadOnlyList<RehearsalSpan> spans, double totalBars)
    {
        List<RehearsalSpan> wanted = [];
        if (track.ByRequest)
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
        double startUnit = timed ? MusicGrid.GetUnit(cue, track.Start) : 0;
        double stopUnit = timed ? MusicGrid.GetUnit(cue, track.Stop) : 0;
        double busyUntil = 0;
        foreach (RehearsalSpan span in wanted)
        {
            bool fresh = !track.ByRequest;
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

    private static double GetBars(MusicCue cue, ClipInfo? info, double fallback)
    {
        if (info is null)
        {
            return fallback;
        }

        double bars = Math.Round(info.Seconds / GetBarSeconds(cue));
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
