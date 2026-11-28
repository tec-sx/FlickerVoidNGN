using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Serialization;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class MusicEditorViewModelTests
{
    /// <summary>The options a game with audio reads its data with, so a layer's conditions have their types as in the editor.</summary>
    private static readonly Lazy<JsonSerializerOptions> GameOptions = new(() =>
    {
        Game game = new(NullLoggerFactory.Instance);
        game.Data.AddResolver(AudioJsonContext.Default);
        return game.Data.Options;
    });

    private int _edits;

    private MusicEditorViewModel Build(MusicCue cue, MusicEditorServices? services = null)
    {
        JsonSerializerOptions options = GameOptions.Value;
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

        cue.Tracks.Should().ContainSingle().Which.Name.Should().Be("Layer 1");
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
    public void GivesEachLayerACardForEachGroupOfItsValues()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tracks = [new MusicTrack { Name = "Bass" }] });
        MusicTrackViewModel layer = editor.Tracks[0];

        layer.Forms.Select(form => form.Title).Should().Equal("Layer", "Plays when asked for", "Clips", "Timing", "Scatter", "Where");
        layer.Forms.Select(form => form.Icon).Should().Equal("Layers", "Tag", "Clip", "Timer", "Scatter", "Where");
        layer.Forms[2].Fields.Select(field => field.Label).Should().Contain("Loop");
        IEnumerable<string> labels = layer.Forms.SelectMany(form => form.Fields).Select(field => field.Label);
        labels.Should().NotContain(["Gain", "Bus", "Icon"], "the gain is set on the header and in the mixer, the bus under Routing and the icon on the panel's header");
        labels.Should().OnlyHaveUniqueItems("a value is edited in one card");
    }

    [Fact]
    public void NamesTheArrangementsOwnCard()
    {
        MusicEditorViewModel editor = Build(new MusicCue());

        editor.Cue.Title.Should().Be("Arrangement");
        editor.Cue.Icon.Should().Be("Music");
    }

    [Fact]
    public void RoutesALayerToABusOrLeavesItOnTheArrangements()
    {
        MusicCue cue = new() { Bus = AudioBus.Music, Tracks = [new MusicTrack { Name = "Rain" }] };
        MusicEditorViewModel editor = Build(cue);
        MusicTrackViewModel layer = editor.Tracks[0];

        layer.BusChoices.Should().Equal("Music", "Ambience", "Effects", "Dialogue", "Interface");
        layer.BusChoice.Should().Be("Music", "a layer goes where its arrangement goes until it is sent elsewhere");
        layer.BusText.Should().Be("to Music bus");

        layer.BusChoice = "Ambience";

        cue.Tracks[0].Bus.Should().Be(AudioBus.Ambience);
        layer.BusText.Should().Be("to Ambience bus");
        _edits.Should().Be(1);

        layer.BusChoice = "Music";

        cue.Tracks[0].Bus.Should().BeNull("the arrangement's own bus is not written, so the layer follows it");
        _edits.Should().Be(2);

        layer.BusChoice = null!;

        _edits.Should().Be(2, "a choice box emptied while its panel changes layers sends nothing");
    }

    [Fact]
    public void ShowsALayerWithItsOwnIconOrOneForItsKind()
    {
        MusicCue cue = new() { Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Dogs", Scatter = "Sounds/Dog.fvsound" }, new MusicTrack { Name = "Drums", Icon = "Drum" }] };
        MusicEditorViewModel editor = Build(cue);

        editor.Tracks.Select(layer => layer.Icon).Should().Equal("Clip", "Scatter", "Drum");
        editor.Tracks[0].IconChoices.Should().Contain(["Clip", "Waveform", "Drum", "Guitar", "Piano", "Trigger", "Dog", "Wind"]);

        editor.Tracks[0].Icon = "Guitar";

        cue.Tracks[0].Icon.Should().Be("Guitar");
        _edits.Should().Be(1);

        editor.Tracks[1].Icon = "Scatter";

        cue.Tracks[1].Icon.Should().BeEmpty("the icon of its kind is not written");
    }

    [Fact]
    public void SaysWhereTheSelectedLayerIs()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Drums" }] });

        editor.Selected = editor.Tracks[1];

        editor.SelectedPlace.Should().Be("Street / layer 2");
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
    public void ListensThroughALongBufferAndSchedulesFarAheadSoABusyEditorDoesNotCrackle()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            ContentManager content = new(folder);

            using MusicPreview preview = new(content);

            preview.Music.Lookahead.Should().Be(MusicPreview.LookaheadFrames);
            MusicPreview.LookaheadFrames.Should().BeGreaterThan(MusicPreview.BufferOptions.Max());
            if (preview.DeviceBufferFrames > 0)
            {
                preview.DeviceBufferFrames.Should().Be(MusicPreview.DefaultBufferFrames);
                preview.SetBuffer(4096);
                preview.DeviceBufferFrames.Should().Be(4096);
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void OffersTheBufferSizesAndTellsTheEditorWhichOneIsChosen()
    {
        int chosen = 2048;
        MusicEditorServices services = new() { BufferFrames = () => chosen, SetBufferFrames = frames => chosen = frames };
        MusicEditorViewModel editor = Build(new MusicCue(), services);

        editor.BufferOptions.Should().Equal(512, 1024, 2048, 4096);
        editor.BufferFrames.Should().Be(2048);

        editor.BufferFrames = 4096;

        chosen.Should().Be(4096);
        editor.BufferText.Should().Be("85 ms");
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
        editor.Tracks[0].StartText.Should().BeEmpty("an arrangement without a tempo has no lines to come in on");
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

    [Fact]
    public void PlacesACueOnceAtTheBarClickedAndSelectsIt()
    {
        MusicCue cue = new() { PhraseBars = 4 };
        MusicEditorViewModel editor = Build(cue);

        editor.AddNotifierAt(9);

        cue.Notifies.Should().ContainSingle().Which.Bar.Should().Be(10);
        cue.Notifies[0].Repeat.Should().BeFalse();
        editor.SelectedCue.Should().BeSameAs(editor.Notifiers[0]);
        editor.SelectedCue!.Repeat = true;
        cue.Notifies[0].Repeat.Should().BeTrue();
        editor.MoveNotifier(editor.Notifiers[0], 14.2);
        cue.Notifies[0].Bar.Should().BeApproximately(15.25f, 0.01f);
        editor.RemoveNotifier(editor.Notifiers[0]);
        editor.SelectedCue.Should().BeNull();
    }

    [Fact]
    public void SelectsARangeOfBarsToPlayInALoop()
    {
        MusicEditorViewModel editor = Build(new MusicCue());

        editor.SetLoopRange(8, 4);

        editor.HasLoopRange.Should().BeTrue();
        editor.LoopStart.Should().Be(4);
        editor.LoopEnd.Should().Be(8);
        editor.LoopPlayback.Should().BeTrue();
        editor.ClearLoopRangeCommand.Execute(null);
        editor.HasLoopRange.Should().BeFalse();
    }

    [Fact]
    public void SaysWhenALayerPlaysAndHowItComesInWithAnIconForIt()
    {
        ConditionSet fromDusk = new();
        fromDusk.Conditions.Add(new FactCondition { Fact = new Tag("Time.Hour"), Compare = FactCompare.GreaterOrEqual, Value = 18 });
        MusicCue cue = new()
        {
            Tempo = 96,
            Tracks =
            [
                new MusicTrack { Name = "Bass", Loop = "Bass.ogg", Start = MusicQuantize.Phrase },
                new MusicTrack { Name = "Drums", Loop = "Drums.ogg", Triggers = [new Tag("Music.Drums"), new Tag("Music.Chase")] },
                new MusicTrack { Name = "Pad", Loop = "Pad.ogg", When = fromDusk },
                new MusicTrack { Name = "Voice", Loop = "Voice.ogg", ByRequest = true, Start = MusicQuantize.None },
                new MusicTrack { Name = "Hits" },
                new MusicTrack { Name = "Dogs", Scatter = "Sounds/Dog.fvsound" },
            ],
        };

        MusicEditorViewModel editor = Build(cue);

        editor.Tracks.Select(layer => layer.PlaysIcon).Should().Equal("Always", "Tag", "Clock", "Tag", "Always", "Always");
        editor.Tracks.Select(layer => layer.PlaysText).Should().Equal("Always", "Music.Drums, Music.Chase", "From 18:00", "On request", "Always", "Always");
        editor.Tracks.Select(layer => layer.StartText).Should().Equal("Phrase", "Bar", "Bar", "At once", "no loop", "scattered");
    }

    [Fact]
    public void WordsTheHoursALayerPlaysIn()
    {
        MusicTrackViewModel.DescribeHours([new RehearsalSpan(0, 24)]).Should().BeEmpty("all day needs no words");
        MusicTrackViewModel.DescribeHours([new RehearsalSpan(18, 24)]).Should().Be("From 18:00");
        MusicTrackViewModel.DescribeHours([new RehearsalSpan(0, 6)]).Should().Be("Until 06:00");
        MusicTrackViewModel.DescribeHours([new RehearsalSpan(8, 17)]).Should().Be("08:00 to 17:00");
        MusicTrackViewModel.DescribeHours([new RehearsalSpan(0, 6), new RehearsalSpan(22, 24)]).Should().Be("22:00 to 06:00", "a night goes over midnight");
        MusicTrackViewModel.DescribeHours([new RehearsalSpan(2, 4), new RehearsalSpan(8, 10)]).Should().Be("Some hours");
        MusicTrackViewModel.DescribeHours([]).Should().Be("Never");
    }

    [Fact]
    public void NumbersTheLayersForTheirColoursAndKeepsTheNumbersAsTheyMove()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Drums" }] });

        editor.Tracks.Select(layer => layer.Index).Should().Equal(0, 1);

        editor.Selected = editor.Tracks[1];
        editor.MoveUpCommand.Execute(null);

        editor.Tracks.Select(layer => layer.Title).Should().Equal("Drums", "Bass");
        editor.Tracks.Select(layer => layer.Index).Should().Equal(0, 1);
    }

    [Fact]
    public void SaysWhatTheRulerCounts()
    {
        Build(new MusicCue { Tempo = 96 }).RulerText.Should().Be("arrangement · bars");
        Build(new MusicCue { Tempo = 0 }).RulerText.Should().Be("arrangement · hours of the day");
    }

    [Fact]
    public void FitsEveryBarIntoTheWidthItIsGiven()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tempo = 96 });

        editor.FitTo(1200);

        editor.BarWidth.Should().Be(50, "24 bars share 1200 pixels");
        editor.FitTo(100);
        editor.BarWidth.Should().Be(10, "bars are never drawn narrower than the zoom allows");
        editor.FitTo(100000);
        editor.BarWidth.Should().Be(160);
    }

    [Fact]
    public void RunsTheDayWhileAnAmbienceArrangementPlaysUnlessItIsHeld()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "Bed.wav"), Wav(48000 * 2));
            ContentManager content = new(folder);
            MusicCue cue = new() { Tempo = 0, Tracks = [new MusicTrack { Name = "Bed", Loop = "Bed.wav" }] };
            using MusicPreview preview = new(content);
            MusicEditorViewModel editor = Build(cue, new MusicEditorServices { Preview = () => preview });
            editor.DayPaces.Select(pace => pace.Label).Should().Equal("10 s an hour", "2 s an hour", "Held");
            editor.DayPace.Should().BeSameAs(editor.DayPaces[0], "the marker moves when an ambience arrangement plays");
            editor.SeekTo(18);
            editor.DayPace = editor.DayPaces[1];

            editor.PlayCommand.Execute(null);
            Pump(editor, 100);

            editor.Playhead.Should().BeApproximately(18.5, 0.001, "a second passed at two seconds an hour");
            editor.PositionText.Should().Be("18:30");

            editor.DayPace = editor.DayPaces[2];
            Pump(editor, 100);

            editor.Playhead.Should().BeApproximately(18.5, 0.001, "the hour is held");
            editor.IsPlaying.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void StopsAtTheEndOfTheDayOrGoesRoundWhenItLoops()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "Bed.wav"), Wav(48000 * 2));
            ContentManager content = new(folder);
            MusicCue cue = new() { Tempo = 0, Tracks = [new MusicTrack { Name = "Bed", Loop = "Bed.wav" }] };
            using MusicPreview preview = new(content);
            MusicEditorViewModel editor = Build(cue, new MusicEditorServices { Preview = () => preview });
            editor.DayPace = editor.DayPaces[1];
            editor.SeekTo(23.75);
            editor.LoopPlayback = true;

            editor.PlayCommand.Execute(null);
            Pump(editor, 100);

            editor.IsPlaying.Should().BeTrue();
            editor.Playhead.Should().BeLessThan(1, "the day went round to midnight");

            editor.LoopPlayback = false;
            editor.SeekTo(23.75);
            Pump(editor, 100);

            editor.IsPlaying.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void MarksTheSelectedLayerSoItsHeaderShowsIt()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Drums" }] });

        editor.Tracks.Select(layer => layer.IsSelected).Should().Equal(true, false);

        editor.Selected = editor.Tracks[1];

        editor.Tracks.Select(layer => layer.IsSelected).Should().Equal(false, true);
    }

    [Fact]
    public void DeletesTheSelectedCueThenTheSelectedClipThenTheLayer()
    {
        MusicCue cue = new() { Tempo = 96, Tracks = [new MusicTrack { Name = "Drums", Loop = "Drums.ogg", FillIn = "DrumsFillIn.ogg", ByRequest = true }] };
        MusicEditorViewModel editor = Build(cue);
        editor.AddNotifierAt(4);
        ArrangementClip fill = new(ArrangementKind.FillIn, 4, 1, 0);
        editor.SelectClip(editor.Tracks[0], fill);
        editor.SelectCue(editor.Notifiers[0]);

        editor.DeleteSelectionCommand.Execute(null);

        cue.Notifies.Should().BeEmpty("the cue is what was picked last");
        cue.Tracks[0].FillIn.Should().Be("DrumsFillIn.ogg");

        editor.DeleteSelectionCommand.Execute(null);

        cue.Tracks[0].FillIn.Should().BeEmpty("the selected clip's file is taken off the layer");
        cue.Tracks[0].Loop.Should().Be("Drums.ogg");
        editor.SelectedClip.Should().BeNull();
        cue.Tracks.Should().ContainSingle();

        editor.DeleteSelectionCommand.Execute(null);

        cue.Tracks.Should().BeEmpty("with no cue and no clip picked, the layer goes");
    }

    [Fact]
    public void ForgetsThePickedCueWhenALayerOrAClipIsPicked()
    {
        MusicCue cue = new() { Tempo = 96, Tracks = [new MusicTrack { Name = "Bass", Loop = "Bass.ogg" }, new MusicTrack { Name = "Drums" }] };
        MusicEditorViewModel editor = Build(cue);
        editor.AddNotifierAt(4);

        editor.SelectLayer(editor.Tracks[1]);

        editor.SelectedCue.Should().BeNull();
        editor.Selected.Should().BeSameAs(editor.Tracks[1]);
        editor.SelectCue(editor.Notifiers[0]);
        editor.SelectClip(editor.Tracks[0], editor.Tracks[0].Clips[0]);
        editor.SelectedCue.Should().BeNull();
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
