using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Serialization;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class MusicEditorViewModelTests
{
    private int _edits;

    private MusicEditorViewModel Build(MusicCue cue, MusicEditorServices? services = null)
    {
        JsonSerializerOptions options = AudioJsonContext.Default.Options;
        JsonTypeInfo cueType = options.GetTypeInfo(typeof(MusicCue));
        JsonTypeInfo trackType = options.GetTypeInfo(typeof(MusicTrack));
        ClipCatalog catalog = new(_ => null);
        return new MusicEditorViewModel("Music/Street.fvmusic", "Street", cue, cueType, trackType, () => _edits++, catalog, services);
    }

    [Fact]
    public void ShowsALanePerTrackAndSelectsTheFirst()
    {
        MusicCue cue = new() { Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Drums", ByRequest = true }] };

        MusicEditorViewModel editor = Build(cue);

        editor.Tracks.Select(track => track.Title).Should().Equal("Bass", "Drums");
        editor.Selected.Should().BeSameAs(editor.Tracks[0]);
        editor.Rows.Should().ContainSingle().Which.Layers.Should().Equal("Drums");
    }

    [Fact]
    public void AddsAndRemovesTracksInTheCueAndReportsTheEdit()
    {
        MusicCue cue = new();
        MusicEditorViewModel editor = Build(cue);

        editor.AddTrackCommand.Execute(null);
        editor.AddTrackCommand.Execute(null);
        editor.MoveUpCommand.Execute(null);
        editor.RemoveSelectedCommand.Execute(null);

        cue.Tracks.Should().ContainSingle().Which.Name.Should().Be("Track 1");
        editor.Tracks.Should().ContainSingle();
        _edits.Should().Be(4);
    }

    [Fact]
    public void LaysTheLanesOutAgainWhenARehearsalSpanMoves()
    {
        MusicCue cue = new() { Tempo = 96, Tracks = [new MusicTrack { Name = "Drums", ByRequest = true, Loop = "Drums.ogg", Bars = 2 }] };
        MusicEditorViewModel editor = Build(cue);
        double before = editor.Tracks[0].Clips.First().Start;

        editor.SetSpan(editor.Rows[0], new RehearsalSpan(8, 16));

        editor.Tracks[0].Clips.First().Start.Should().BeGreaterThan(before);
    }

    [Fact]
    public void GivesEachTrackSectionsForItsValues()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tracks = [new MusicTrack { Name = "Bass" }] });

        editor.Tracks[0].Forms.Select(form => form.Title).Should().Equal("Track", "Clips", "Timing", "Mix", "Scatter");
        editor.Tracks[0].Forms[1].Fields.Select(field => field.Label).Should().Contain("Loop");
    }

    [Fact]
    public void MakesARehearsalRowOfEachSceneAreaThatAsksForATrack()
    {
        MusicCue cue = new() { Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Drums", ByRequest = true }] };
        MusicEditorServices services = new()
        {
            Areas = () => [new RehearsalSource("Inn Room Cue", ["Drums"]), new RehearsalSource("Window Spot", ["Strings"])],
        };

        MusicEditorViewModel editor = Build(cue, services);

        editor.Rows.Select(row => row.Label).Should().Equal("Rehearsal · Inn Room Cue");
        editor.Rows[0].IsArea.Should().BeTrue();
    }

    [Fact]
    public void SnapsTheSeekToTheGridAndShowsThePosition()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tempo = 96 });

        editor.SeekTo(11.62);

        editor.Playhead.Should().Be(12);
        editor.PositionText.Should().Be("13.1.1");
        editor.Snap = MusicQuantize.Beat;
        editor.SeekTo(11.62);
        editor.PositionText.Should().Be("12.3.1");
    }

    [Fact]
    public void ConvertsATracksGainToDecibelsAndBack()
    {
        MusicCue cue = new() { Tracks = [new MusicTrack { Name = "Bass", Gain = 1 }] };
        MusicEditorViewModel editor = Build(cue);

        editor.Tracks[0].GainDb = -6;

        cue.Tracks[0].Gain.Should().BeApproximately(0.5012f, 0.001f);
        editor.Tracks[0].GainText.Should().Be("-6.0");
        _edits.Should().Be(1);
    }

    [Fact]
    public void AddsMovesAndRemovesNotifiersOnThePhrase()
    {
        MusicCue cue = new() { PhraseBars = 4 };
        MusicEditorViewModel editor = Build(cue);

        editor.AddNotifierAt(1.5);
        editor.MoveNotifier(editor.Notifiers[0], 3.1);

        cue.Notifies.Should().ContainSingle().Which.Bar.Should().BeApproximately(4.0f, 0.01f);
        editor.RemoveNotifier(editor.Notifiers[0]);
        cue.Notifies.Should().BeEmpty();
    }

    [Fact]
    public void AssignsADroppedSoundAsALoopOrAFillByItsName()
    {
        MusicCue cue = new() { Tracks = [new MusicTrack { Name = "Drums" }] };
        MusicEditorViewModel editor = Build(cue);

        editor.DropSound("Music/DrumsLoop.ogg", 0);
        editor.DropSound("Music/DrumsFillIn.ogg", 0);
        editor.DropSound("Music/Pad.ogg", 1);

        cue.Tracks[0].Loop.Should().Be("Music/DrumsLoop.ogg");
        cue.Tracks[0].FillIn.Should().Be("Music/DrumsFillIn.ogg");
        cue.Tracks.Select(track => track.Name).Should().Equal("Drums", "Pad");
    }

    [Fact]
    public void ListsTurnedOnMixStatesAndWordsWhatTheyDo()
    {
        MusicEditorViewModel editor = Build(new MusicCue());

        editor.MixStates.Select(state => state.Name).Should().Contain(["Indoors", "Talking", "Hub open", "Paused"]);
        editor.MixStates.First(state => state.Name == "Indoors").Summary.Should().Contain("Ambience dulled to 1.2 kHz");
    }

    [Fact]
    public void PlaysTheCuePastItsFirstBarsThroughThePreviewAndStops()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "Loop.wav"), Wav(48000 * 2));
            ContentManager content = new(folder);
            MusicCue cue = new() { Tempo = 120, Tracks = [new MusicTrack { Name = "Bass", Loop = "Loop.wav" }] };
            using MusicPreview preview = new(content);
            MusicEditorServices services = new() { Preview = () => preview };
            MusicEditorViewModel editor = Build(cue, services);

            editor.PlayCommand.Execute(null);
            for (int tick = 0; tick < 400; tick++)
            {
                editor.Tick(TimeSpan.FromMilliseconds(10));
                Thread.Sleep(6);
            }

            editor.IsPlaying.Should().BeTrue();
            editor.Playhead.Should().BeGreaterThan(0.3);
            editor.LayersText.Should().StartWith("1 of 1");
            editor.StopCommand.Execute(null);
            editor.IsPlaying.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ShowsAnAmbienceCueOnTheHoursOfTheDay()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tempo = 0, Tracks = [new MusicTrack { Name = "Bed", Loop = "Bed.ogg" }] });

        editor.IsHours.Should().BeTrue();
        editor.TotalBars.Should().Be(24);
        editor.SeekTo(18.5);
        editor.PositionText.Should().Be("18:30");
        editor.TimeText.Should().Be("18:30");
        Build(new MusicCue()).IsHours.Should().BeFalse();
    }

    [Fact]
    public void KeepsSectionsAndContinuePointsInTheCue()
    {
        MusicCue cue = new();
        MusicEditorViewModel editor = Build(cue);

        editor.AddSectionCommand.Execute(null);
        editor.AddSectionCommand.Execute(null);
        editor.Sections[1].Bars = 8;
        editor.SeekTo(8);
        editor.AddContinuePointCommand.Execute(null);

        cue.Sections.Select(section => section.Bars).Should().Equal(4, 8);
        cue.Sections.Select(section => section.Name).Should().Equal("Section 1", "Section 2");
        cue.ContinuePoints.Should().ContainSingle().Which.Bar.Should().Be(9);
        editor.SectionBands.Select(band => band.Start).Should().Equal(0, 4);
        editor.RemoveSection(editor.Sections[0]);
        cue.Sections.Should().ContainSingle();
    }

    [Fact]
    public void ShowsTheCueStackWhileAnotherCueIsPushedOverIt()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Music"));
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "Loop.wav"), Wav(48000 * 2));
            File.WriteAllText(Path.Combine(folder, "Music", "Hub.fvmusic"), """{ "type": "music", "tempo": 120, "tracks": [ { "name": "Song", "loop": "Loop.wav" } ] }""");
            ContentManager content = new(folder);
            MusicCue cue = new() { Tempo = 120, Tracks = [new MusicTrack { Name = "Bass", Loop = "Loop.wav" }] };
            using MusicPreview preview = new(content);
            MusicEditorViewModel editor = Build(cue, new MusicEditorServices { Preview = () => preview });
            editor.PlayCommand.Execute(null);
            Pump(editor, 60);

            editor.PushCueCommand.Execute("Music/Hub.fvmusic");
            Pump(editor, 250);

            editor.StackEntries.Select(entry => entry.Name).Should().Equal("Music/Hub.fvmusic", "Music/Street.fvmusic");
            editor.StackEntries[1].State.Should().StartWith("held at bar");
            editor.PopCueCommand.Execute(null);
            Pump(editor, 400);
            editor.StackEntries.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void WordsWhatEachTrackIsWaitingForInTheLiveRows()
    {
        MusicCue cue = new()
        {
            Tracks =
            [
                new MusicTrack { Name = "Bass" },
                new MusicTrack { Name = "Drums", ByRequest = true },
                new MusicTrack { Name = "Voice", ByRequest = true },
            ],
        };
        MusicEditorServices services = new() { Areas = () => [new RehearsalSource("Inn Room Cue", ["Drums"])] };

        MusicEditorViewModel editor = Build(cue, services);

        editor.LiveRows.Select(row => row.Name).Should().Equal("Bass", "Drums", "Voice");
        editor.LiveRows.Select(row => row.State).Should().Equal("plays always", "in Inn Room Cue", "asked by script");
        editor.LiveRows.Select(row => row.IsOn).Should().OnlyContain(on => !on);
    }

    private static void Pump(MusicEditorViewModel editor, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            editor.Tick(TimeSpan.FromMilliseconds(10));
            Thread.Sleep(6);
        }
    }

    private static byte[] Wav(int frames)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        int bytes = frames * 2;
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + bytes);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(48000);
        writer.Write(96000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(bytes);
        for (int frame = 0; frame < frames; frame++)
        {
            writer.Write((short)(frame % 100 * 100));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
