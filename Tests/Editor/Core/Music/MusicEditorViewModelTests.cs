using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Serialization;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class MusicEditorViewModelTests
{
    private int _edits;

    private MusicEditorViewModel Build(MusicCue cue)
    {
        JsonSerializerOptions options = AudioJsonContext.Default.Options;
        JsonTypeInfo cueType = options.GetTypeInfo(typeof(MusicCue));
        JsonTypeInfo trackType = options.GetTypeInfo(typeof(MusicTrack));
        ClipCatalog catalog = new(_ => null);
        return new MusicEditorViewModel("Street", cue, cueType, trackType, () => _edits++, catalog);
    }

    [Fact]
    public void ShowsALanePerTrackAndSelectsTheFirst()
    {
        MusicCue cue = new() { Tracks = [new MusicTrack { Name = "Bass" }, new MusicTrack { Name = "Drums", ByRequest = true }] };

        MusicEditorViewModel editor = Build(cue);

        editor.Tracks.Select(track => track.Title).Should().Equal("Bass", "Drums");
        editor.Selected.Should().BeSameAs(editor.Tracks[0]);
        editor.HasRehearsal.Should().BeTrue();
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

        editor.SetSpan(0, new RehearsalSpan(8, 16));

        editor.Tracks[0].Clips.First().Start.Should().BeGreaterThan(before);
    }

    [Fact]
    public void GivesEachTrackSectionsForItsValues()
    {
        MusicEditorViewModel editor = Build(new MusicCue { Tracks = [new MusicTrack { Name = "Bass" }] });

        editor.Tracks[0].Forms.Select(form => form.Title).Should().Equal("Track", "Clips", "Timing", "Mix", "Scatter");
        editor.Tracks[0].Forms[1].Fields.Select(field => field.Label).Should().Contain("Loop");
    }
}
