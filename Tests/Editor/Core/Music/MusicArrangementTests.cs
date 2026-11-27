using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class MusicArrangementTests
{
    private static readonly MusicCue Cue = new() { Tempo = 96, BeatsPerBar = 4, PhraseBars = 4 };

    private static void ShouldMatch(IReadOnlyList<ArrangementClip> actual, params ArrangementClip[] expected)
    {
        actual.Should().HaveSameCount(expected);
        for (int index = 0; index < expected.Length; index++)
        {
            actual[index].Kind.Should().Be(expected[index].Kind);
            actual[index].Repeat.Should().Be(expected[index].Repeat);
            actual[index].Start.Should().BeApproximately(expected[index].Start, 1e-6);
            actual[index].Length.Should().BeApproximately(expected[index].Length, 1e-6);
        }
    }

    [Fact]
    public void LoopsATrackWithoutARequestThroughTheWholeCue()
    {
        MusicTrack track = new() { Name = "Bass" };

        IReadOnlyList<ArrangementClip> clips = MusicArrangement.Plan(Cue, track, new TrackLengths(4, 0, 0), [], 10);

        ShouldMatch(clips,
            new ArrangementClip(ArrangementKind.Loop, 0, 4, 0),
            new ArrangementClip(ArrangementKind.Loop, 4, 4, 1),
            new ArrangementClip(ArrangementKind.Loop, 8, 2, 2));
    }

    [Fact]
    public void BringsARequestedTrackInOnTheNextBarAfterItsFillIn()
    {
        MusicTrack track = new() { Name = "Drums", ByRequest = true };

        IReadOnlyList<ArrangementClip> clips = MusicArrangement.Plan(Cue, track, new TrackLengths(2, 1, 1), [new RehearsalSpan(6.4, 11.2)], 16);

        ShouldMatch(clips,
            new ArrangementClip(ArrangementKind.Wait, 6.4, 0.6, 0),
            new ArrangementClip(ArrangementKind.FillIn, 7, 1, 0),
            new ArrangementClip(ArrangementKind.Loop, 8, 2, 0),
            new ArrangementClip(ArrangementKind.Loop, 10, 2, 1),
            new ArrangementClip(ArrangementKind.FillOut, 12, 1, 0));
    }

    [Fact]
    public void WaitsForTheNextPhraseWhenTheTrackQuantizesToPhrases()
    {
        MusicTrack track = new() { Name = "Pad", ByRequest = true, Start = MusicQuantize.Phrase, Stop = MusicQuantize.Phrase };

        IReadOnlyList<ArrangementClip> clips = MusicArrangement.Plan(Cue, track, new TrackLengths(4, 0, 0), [new RehearsalSpan(5.5, 13)], 20);

        clips[0].Should().Be(new ArrangementClip(ArrangementKind.Wait, 5.5, 2.5, 0));
        clips[1].Should().Be(new ArrangementClip(ArrangementKind.Loop, 8, 4, 0));
        clips[^1].Should().Be(new ArrangementClip(ArrangementKind.Loop, 12, 4, 1));
    }

    [Fact]
    public void NeverStartsTheLoopOfATrackNoLongerWantedDuringItsFillIn()
    {
        MusicTrack track = new() { Name = "Drums", ByRequest = true };

        IReadOnlyList<ArrangementClip> clips = MusicArrangement.Plan(Cue, track, new TrackLengths(2, 2, 1), [new RehearsalSpan(4.2, 5.5)], 16);

        clips.Select(clip => clip.Kind).Should().Equal(ArrangementKind.Wait, ArrangementKind.FillIn);
    }

    [Fact]
    public void KeepsASecondSpanFromStartingBeforeTheFirstHasStopped()
    {
        MusicTrack track = new() { Name = "Drums", ByRequest = true };

        IReadOnlyList<ArrangementClip> clips = MusicArrangement.Plan(Cue, track, new TrackLengths(1, 0, 0), [new RehearsalSpan(1, 3.5), new RehearsalSpan(3.6, 6)], 10);

        clips.Where(clip => clip.Kind == ArrangementKind.Loop).Select(clip => clip.Start).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void StartsAtOnceInACueWithoutATempo()
    {
        MusicCue ambience = new() { Tempo = 0 };
        MusicTrack track = new() { Name = "Bed", ByRequest = true };

        IReadOnlyList<ArrangementClip> clips = MusicArrangement.Plan(ambience, track, new TrackLengths(2, 0, 0), [new RehearsalSpan(1.3, 4)], 8);

        ShouldMatch(clips,
            new ArrangementClip(ArrangementKind.Loop, 1.3, 2, 0),
            new ArrangementClip(ArrangementKind.Loop, 3.3, 0.7, 1));
    }

    [Fact]
    public void ScattersMarksAcrossTheTimeATrackIsWanted()
    {
        MusicTrack track = new() { Scatter = "Sounds/Bark.fvdata", EveryMin = 10, EveryMax = 10 };

        IReadOnlyList<ArrangementClip> clips = MusicArrangement.Plan(Cue, track, new TrackLengths(0, 0, 0), [], 24);

        clips.Should().OnlyContain(clip => clip.Kind == ArrangementKind.Scatter && clip.Length == 0);
        clips.Should().HaveCountGreaterThan(5);
        clips.Select(clip => clip.Start).Should().BeInAscendingOrder().And.OnlyContain(start => start >= 0 && start <= 24);
    }

    [Fact]
    public void MeasuresTracksFromTheirFilesRoundedToWholeBars()
    {
        MusicTrack loop = new() { Name = "Bass", Loop = "Bass.ogg", FillIn = "In.ogg" };
        MusicTrack fixedLength = new() { Name = "Pad", Loop = "Pad.ogg", Bars = 8 };
        ClipCatalog catalog = new(file => file == "In.ogg" ? Clip(2.5) : Clip(10));

        TrackLengths measured = MusicArrangement.Measure(Cue, loop, catalog);
        TrackLengths set = MusicArrangement.Measure(Cue, fixedLength, catalog);

        measured.Should().Be(new TrackLengths(4, 1, 0));
        set.LoopBars.Should().Be(8);
    }

    private static FlickerVoid.Engine.Audio.Mixing.SoundClip Clip(double seconds)
    {
        float[] samples = new float[(int)(seconds * 48000)];
        Array.Fill(samples, 0.5f);
        return new FlickerVoid.Engine.Audio.Mixing.SoundClip(samples, 1, 48000);
    }
}
