using System.Numerics;
using System.Text.Json.Nodes;

using Arch.Core;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Saves;

namespace FlickerVoid.Engine.Audio.Tests.Music;

public sealed class MusicSystemTests : IDisposable
{
    private const float Close = 0.002f;
    private const string Bass = """{ "name": "Bass", "loop": "Bass.wav" }""";

    private readonly MusicSystemTestsBuilder _builder = new();

    public MusicSystemTests()
    {
        _builder.WithClip("Bass", 0.1f);
        _builder.WithClip("Drums", 0.2f);
        _builder.WithClip("FillIn", 0.4f);
        _builder.WithClip("FillOut", 0.05f);
        _builder.WithClip("TwoBars", 0.2f, 0.3f);
    }

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void PlaysATrackFromTheCuesFirstBar()
    {
        string cue = _builder.WithCue("Street", Bass);

        long first = Start(cue);
        _builder.PlayUntilBar(2.6);

        _builder.HeardAt(first - 1).Should().Be(0);
        _builder.HeardAt(first).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 2.5).Should().BeApproximately(0.1f, Close);
        _builder.Music.GetCurrent().Should().Be(cue);
    }

    [Fact]
    public void BringsAnAskedTrackInOnTheNextBarAfterItsFillIn()
    {
        string cue = _builder.WithCue("Street", Bass + """, { "name": "Drums", "loop": "Drums.wav", "fillIn": "FillIn.wav", "byRequest": true }""");
        long first = Start(cue);
        _builder.PlayUntilBar(1.5);

        _builder.Music.RequestLayer("Drums");
        _builder.PlayUntilBar(4);

        _builder.HeardAtBar(first, 1.9).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 2).Should().BeApproximately(0.5f, Close);
        _builder.HeardAtBar(first, 2.9).Should().BeApproximately(0.5f, Close);
        _builder.HeardAtBar(first, 3).Should().BeApproximately(0.3f, Close);
        _builder.Music.IsLayerOn("Drums").Should().BeTrue();
    }

    [Fact]
    public void TakesATrackOutOnTheNextBarWithItsFillOut()
    {
        string cue = _builder.WithCue("Street", Bass + """, { "name": "Drums", "loop": "Drums.wav", "fillOut": "FillOut.wav", "byRequest": true }""");
        _builder.Music.RequestLayer("Drums");
        long first = Start(cue);
        _builder.PlayUntilBar(2.5);

        _builder.Music.ReleaseLayer("Drums");
        _builder.PlayUntilBar(5);

        _builder.HeardAtBar(first, 1.5).Should().BeApproximately(0.3f, Close);
        _builder.HeardAtBar(first, 2.99).Should().BeApproximately(0.3f, Close);
        _builder.HeardAtBar(first, 3.5).Should().BeApproximately(0.15f, Close);
        _builder.HeardAtBar(first, 4.5).Should().BeApproximately(0.1f, Close);
        _builder.Music.IsLayerOn("Drums").Should().BeFalse();
    }

    [Fact]
    public void NeverStartsTheLoopOfATrackNoLongerWantedDuringItsFillIn()
    {
        string cue = _builder.WithCue("Street", Bass + """, { "name": "Drums", "loop": "Drums.wav", "fillIn": "FillIn.wav", "byRequest": true }""");
        long first = Start(cue);
        _builder.PlayUntilBar(1.5);
        _builder.Music.RequestLayer("Drums");
        _builder.PlayUntilBar(2.5);

        _builder.Music.ReleaseLayer("Drums");
        _builder.PlayUntilBar(4);

        _builder.HeardAtBar(first, 2.9).Should().BeApproximately(0.5f, Close);
        _builder.HeardAtBar(first, 3.5).Should().BeApproximately(0.1f, Close);
    }

    [Theory]
    [InlineData("Locked", 0.4f, 0.3f)]
    [InlineData("FromStart", 0.3f, 0.4f)]
    public void StartsALoopInStepWithTheCueOrFromItsStart(string phase, float second, float third)
    {
        string cue = _builder.WithCue("Street", Bass + $$""", { "name": "Lead", "loop": "TwoBars.wav", "phase": "{{phase}}", "byRequest": true }""");
        long first = Start(cue);
        _builder.PlayUntilBar(1.5);

        _builder.Music.RequestLayer("Lead");
        _builder.PlayUntilBar(4);

        _builder.HeardAtBar(first, 2.5).Should().BeApproximately(second, Close);
        _builder.HeardAtBar(first, 3.5).Should().BeApproximately(third, Close);
    }

    [Fact]
    public void WaitsForTheNextPhrase()
    {
        string cue = _builder.WithCue("Street", Bass + """, { "name": "Drums", "loop": "Drums.wav", "start": "Phrase", "byRequest": true }""");
        long first = Start(cue);
        _builder.PlayUntilBar(1.5);

        _builder.Music.RequestLayer("Drums");
        _builder.PlayUntilBar(6);

        _builder.HeardAtBar(first, 4.9).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 5.1).Should().BeApproximately(0.3f, Close);
    }

    [Fact]
    public void WaitsForTheNextBeat()
    {
        string cue = _builder.WithCue("Street", Bass + """, { "name": "Drums", "loop": "Drums.wav", "start": "Beat", "byRequest": true }""");
        long first = Start(cue);
        _builder.PlayUntilBar(1.1);

        _builder.Music.RequestLayer("Drums");
        _builder.PlayUntilBar(2);

        _builder.HeardAtBar(first, 1.24).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 1.26).Should().BeApproximately(0.3f, Close);
    }

    [Fact]
    public void PlaysATrackWhileItsConditionsHold()
    {
        string cue = _builder.WithCue("Street", Bass + """, { "name": "Pad", "loop": "Drums.wav", "when": { "conditions": [ { "$type": "fact", "fact": "Time.Dusk" } ] } }""");
        long first = Start(cue);
        _builder.PlayUntilBar(1.5);

        _builder.Game.Facts.SetFact(new Tag("Time.Dusk"), 1);
        _builder.PlayUntilBar(3);

        _builder.HeardAtBar(first, 1.5).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 2.5).Should().BeApproximately(0.3f, Close);
    }

    [Fact]
    public void PlaysAMusicAreasTracksWhileSomeoneStandsInIt()
    {
        string cue = _builder.WithCue("Street", Bass + """, { "name": "Drums", "loop": "Drums.wav", "byRequest": true }""");
        _builder.Spawn("""{ "name": "Inn Room Music", "components": { "transform": {}, "triggerArea": { "size": [4, 4, 4] }, "musicArea": { "layers": [ "Drums" ] } } }""");
        Entity fox = _builder.Spawn("""{ "name": "Fox", "components": { "transform": { "position": [10, 0, 0] }, "triggerActivator": {} } }""");
        long first = Start(cue);
        _builder.PlayUntilBar(1.5);

        Move(fox, 0);
        _builder.PlayUntilBar(3.5);
        Move(fox, 10);
        _builder.PlayUntilBar(5);

        _builder.HeardAtBar(first, 1.5).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 2.5).Should().BeApproximately(0.3f, Close);
        _builder.HeardAtBar(first, 4.5).Should().BeApproximately(0.1f, Close);
    }

    [Fact]
    public void TakesTheCuesPlaceOnItsNextBar()
    {
        string street = _builder.WithCue("Street", Bass);
        string cellar = _builder.WithCue("Cellar", """{ "name": "Lead", "loop": "TwoBars.wav" }""");
        long first = Start(street);
        _builder.PlayUntilBar(1.5);

        _builder.Music.Play(cellar);
        _builder.PlayFor(MusicSystemTestsBuilder.BarFrames * 2);

        _builder.HeardAtBar(first, 1.9).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 2.5).Should().BeApproximately(0.2f, Close);
        _builder.HeardAtBar(first, 3.5).Should().BeApproximately(0.3f, Close);
        _builder.Music.GetCurrent().Should().Be(cellar);
    }

    [Fact]
    public void HoldsTheCueBelowAPushedOneAndContinuesItWhereItStopped()
    {
        string street = _builder.WithCue("Street", """{ "name": "Lead", "loop": "TwoBars.wav" }""");
        _builder.WithClip("Song", 0.6f);
        string hub = _builder.WithCue("Hub", """{ "name": "Song", "loop": "Song.wav" }""", "\"tempo\": 480, \"fillIn\": \"FillIn.wav\", \"fillOut\": \"FillOut.wav\"");
        long first = Start(street);
        _builder.PlayUntilBar(2.5);

        _builder.Music.Push(hub);
        _builder.PlayFor(MusicSystemTestsBuilder.BarFrames * 3);
        string? onTop = _builder.Music.GetCurrent();
        _builder.Music.Pop();
        _builder.PlayFor(MusicSystemTestsBuilder.BarFrames * 4);

        onTop.Should().Be(hub);
        _builder.HeardAtBar(first, 3.5).Should().BeApproximately(0.6f, Close, "the fill-in plays over the street's third bar");
        _builder.HeardAtBar(first, 4.5).Should().BeApproximately(0.6f, Close, "the song plays alone");
        _builder.HeardAtBar(first, 5.5).Should().BeApproximately(0.6f, Close);
        _builder.HeardAtBar(first, 6.5).Should().BeApproximately(0.05f, Close, "the fill-out plays alone");
        _builder.HeardAtBar(first, 7.5).Should().BeApproximately(0.3f, Close, "the street continues with its fourth bar");
        _builder.HeardAtBar(first, 8.5).Should().BeApproximately(0.2f, Close);
        _builder.Music.GetCurrent().Should().Be(street);
    }

    [Fact]
    public void StopsOnTheNextBar()
    {
        string cue = _builder.WithCue("Street", Bass);
        long first = Start(cue);
        _builder.PlayUntilBar(1.5);

        _builder.Music.Stop();
        _builder.PlayFor(MusicSystemTestsBuilder.BarFrames * 2);

        _builder.HeardAtBar(first, 1.99).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 2.5).Should().Be(0);
        _builder.Music.GetCurrent().Should().BeNull();
    }

    [Fact]
    public void FadesTheTracksOfACueWithoutATempoInAtOnce()
    {
        string cue = _builder.WithCue("Ambience", """{ "name": "Bed", "loop": "Bass.wav", "fadeIn": 0.5 }""", "\"tempo\": 0, \"bus\": \"Ambience\"");

        _builder.Music.Play(cue);
        _builder.PlayFor(40000);

        long start = MusicSystem.LookaheadFrames;
        _builder.HeardAt(start + 12000).Should().BeApproximately(0.05f, 0.005f);
        _builder.HeardAt(start + 30000).Should().BeApproximately(0.1f, Close);
        _builder.Music.GetCurrent(AudioBus.Ambience).Should().Be(cue);
    }

    [Fact]
    public void ThrowsAScatterTracksSoundsAroundTheListener()
    {
        _builder.Audio.SpawnListener();
        string bark = _builder.Audio.WithSound("Bark", "\"bus\": \"Ambience\", \"maxDistance\": 100", 0.5f);
        string cue = _builder.WithCue("Ambience", $$"""{ "name": "Dogs", "scatter": "{{bark}}", "everyMin": 0.5, "everyMax": 0.5, "nearDistance": 2, "farDistance": 4 }""", "\"tempo\": 0, \"bus\": \"Ambience\"");

        _builder.Music.Play(cue);
        _builder.PlayFor(48000);

        _builder.HeardAt(12000).Should().Be(0);
        MathF.Abs(_builder.HeardAt(40000)).Should().BeGreaterThan(0);
    }

    [Fact]
    public void PlaysAStingerOnTheNextBeat()
    {
        string cue = _builder.WithCue("Street", Bass);
        string sting = _builder.WithClip("Sting", 0.5f);
        long first = Start(cue);
        _builder.PlayUntilBar(1.1);

        _builder.Music.PlayStinger(sting);
        _builder.PlayUntilBar(2);

        _builder.HeardAtBar(first, 1.24).Should().BeApproximately(0.1f, Close);
        _builder.HeardAtBar(first, 1.26).Should().BeApproximately(0.6f, Close);
    }

    [Fact]
    public void SendsTheBeatsItReaches()
    {
        string cue = _builder.WithCue("Street", Bass);
        List<MusicBeatEvent> beats = [];
        _builder.Game.Events.Handle((in MusicBeatEvent beat) => beats.Add(beat));
        Start(cue);

        _builder.PlayUntilBar(2.1);
        _builder.PlayFor(1000);

        beats.Should().StartWith([new MusicBeatEvent(cue, 1, 1), new MusicBeatEvent(cue, 1, 2), new MusicBeatEvent(cue, 1, 3), new MusicBeatEvent(cue, 1, 4), new MusicBeatEvent(cue, 2, 1)]);
        _builder.Music.GetPosition().Bar.Should().Be(2);
    }

    [Fact]
    public void ContinuesASavedCueFromTheBarItHadReached()
    {
        string cue = _builder.WithCue("Street", """{ "name": "Lead", "loop": "TwoBars.wav" }""");
        Start(cue);
        _builder.PlayUntilBar(2.5);
        GlobalSaveState saved = _builder.Game.SaveStates.Globals.Single(state => state.Key == "music");
        JsonNode? node = saved.Write();
        _builder.Music.Stop();
        _builder.PlayFor(MusicSystemTestsBuilder.BarFrames * 2);

        saved.Read(node);
        long restored = _builder.Position + MusicSystem.LookaheadFrames;
        _builder.PlayFor(MusicSystemTestsBuilder.BarFrames * 2);

        _builder.Music.GetCurrent().Should().Be(cue);
        _builder.HeardAt(restored + 12000).Should().BeApproximately(0.3f, Close, "it had played one whole bar, so it continues with its second");
        _builder.HeardAt(restored + 36000).Should().BeApproximately(0.2f, Close);
    }

    [Fact]
    public void PlaysASceneMusicsCuesWhenItsEntityIsSpawned()
    {
        string cue = _builder.WithCue("Street", Bass);
        string ambience = _builder.WithCue("StreetAmbience", """{ "name": "Bed", "loop": "Drums.wav" }""", "\"tempo\": 0, \"bus\": \"Ambience\"");

        _builder.Spawn($$"""{ "name": "Street Music", "components": { "sceneMusic": { "music": "{{cue}}", "ambience": "{{ambience}}" } } }""");
        _builder.PlayFor(4800);

        _builder.Music.GetCurrent().Should().Be(cue);
        _builder.Music.GetCurrent(AudioBus.Ambience).Should().Be(ambience);
        _builder.HeardAt(4000).Should().BeApproximately(0.3f, Close);
    }

    /// <summary>Plays a cue through its first frame and returns the frame its first bar begins on.</summary>
    [Fact]
    public void StretchesATracksFilesFromItsOwnTempoToTheCuesWithoutChangingTheirLength()
    {
        _builder.WithClip("Slow", 0.2f, 0.4f);
        string cue = _builder.WithCue("Street", """{ "name": "Slow", "loop": "Slow.wav", "tempo": 240 }""");

        long first = Start(cue);
        _builder.PlayUntilBar(3);

        _builder.HeardAtBar(first, 1.25).Should().BeApproximately(0.2f, Close);
        _builder.HeardAtBar(first, 1.75).Should().BeApproximately(0.4f, Close);
        _builder.HeardAtBar(first, 2.25).Should().BeApproximately(0.2f, Close);
        _builder.HeardAtBar(first, 2.75).Should().BeApproximately(0.4f, Close);
    }

    private long Start(string cue)
    {
        _builder.Music.Play(cue);
        _builder.PlayFor(1);
        return _builder.Music.GetFrameOfBar(1);
    }

    private void Move(Entity entity, float x)
    {
        Transform transform = _builder.Game.World.Get<Transform>(entity);
        transform.Position = new Vector3(x, 0, 0);
        _builder.Game.World.Set(entity, transform);
    }
}
