using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Tests.Sounds;

public sealed class AudioSystemTests : IDisposable
{
    private const string Flat = "\"spatial\": false";

    private readonly AudioSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void PlaysASoundAsItIsAtItsGain()
    {
        string sound = _builder.WithSound("Chime", "\"spatial\": false, \"gain\": 0.5", 0.5f);

        SoundHandle handle = _builder.Audio.Play(sound);

        (float left, float right) = _builder.Hear();
        handle.IsNone.Should().BeFalse();
        left.Should().BeApproximately(0.25f, 0.001f);
        right.Should().BeApproximately(0.25f, 0.001f);
    }

    [Fact]
    public void HearsASoundOnTheRightLouderInTheRightEar()
    {
        _builder.SpawnListener();
        string sound = _builder.WithSound("Knock", string.Empty, 0.5f);

        _builder.Audio.Play(sound, new Vector3(3, 0, 0));

        (float left, float right) = _builder.Hear();
        right.Should().BeGreaterThan(left * 2);
        left.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GivesASoundItsHeadRelatedDirectionThroughSteamAudio()
    {
        Assert.SkipUnless(_builder.UseSteamAudio(), "Steam Audio's library is not built here.");
        _builder.SpawnListener();
        string sound = _builder.WithNoise("Hiss");

        _builder.Audio.Play(sound, new Vector3(3, 0, 0));

        (double left, double right) = _builder.HearEnergy();
        right.Should().BeGreaterThan(left * 1.5);
    }

    [Fact]
    public void TurnsTheEarsWithTheCamera()
    {
        _builder.SpawnListener();
        _builder.Spawn("""{ "name": "Camera", "components": { "transform": { "position": [0, 5, -5], "rotation": [0, 1, 0, 0] }, "camera": {} } }""");
        string sound = _builder.WithSound("Knock", string.Empty, 0.5f);

        _builder.Audio.Play(sound, new Vector3(3, 0, 0));

        (float left, float right) = _builder.Hear();
        left.Should().BeGreaterThan(right * 2);
    }

    [Fact]
    public void HearsFromTheCameraWithoutAListener()
    {
        _builder.Spawn("""{ "name": "Camera", "components": { "transform": { "position": [100, 0, 0] }, "camera": {} } }""");
        string sound = _builder.WithSound("Knock", string.Empty, 0.5f);

        _builder.Audio.Play(sound, new Vector3(100, 0, -0.5f));

        (float left, _) = _builder.Hear();
        left.Should().BeGreaterThan(0.3f);
    }

    [Fact]
    public void FollowsTheEntityItPlaysOn()
    {
        _builder.SpawnListener();
        Entity cart = _builder.Spawn("""{ "name": "Cart", "components": { "transform": { "position": [3, 0, 0] } } }""");
        string sound = _builder.WithSound("Rattle", "\"loop\": true", 0.5f);
        _builder.Audio.Play(sound, cart);
        _builder.Hear();

        ref Transform transform = ref _builder.Game.World.Get<Transform>(cart);
        transform.Position = new Vector3(-3, 0, 0);
        _builder.Run();

        (float left, float right) = _builder.Hear();
        left.Should().BeGreaterThan(right * 2);
    }

    [Fact]
    public void FadesASoundOutOfHearingWithDistance()
    {
        _builder.SpawnListener();
        string sound = _builder.WithSound("Knock", "\"maxDistance\": 10", 0.5f);

        _builder.Audio.Play(sound, new Vector3(0, 0, -12));

        (float left, float right) = _builder.Hear();
        left.Should().Be(0);
        right.Should().Be(0);
    }

    [Fact]
    public void DropsAPlayInsideItsThrottle()
    {
        string sound = _builder.WithSound("Step", "\"spatial\": false, \"throttle\": 1", 0.5f);

        SoundHandle first = _builder.Audio.Play(sound);
        SoundHandle tooSoon = _builder.Audio.Play(sound);
        _builder.Run(1.1f);
        SoundHandle later = _builder.Audio.Play(sound);

        first.IsNone.Should().BeFalse();
        tooSoon.IsNone.Should().BeTrue();
        later.IsNone.Should().BeFalse();
    }

    [Fact]
    public void NeverPlaysTheSameVariationTwiceRunning()
    {
        string sound = _builder.WithSound("Step", Flat, 0.25f, 0.5f, 0.75f);
        List<float> heard = [];

        for (int play = 0; play < 30; play++)
        {
            SoundHandle handle = _builder.Audio.Play(sound);
            (float left, _) = _builder.Hear();
            heard.Add(left);
            _builder.Audio.Stop(handle, 0);
            _builder.Hear();
        }

        for (int play = 1; play < heard.Count; play++)
        {
            heard[play].Should().NotBeApproximately(heard[play - 1], 0.01f);
        }

        heard.Select(value => MathF.Round(value, 2)).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void VariesGainAndPitchWithinTheirRanges()
    {
        string sound = _builder.WithSound("Step", "\"spatial\": false, \"gainVariation\": 0.5, \"pitchVariation\": 0.2", 0.5f);
        List<float> heard = [];

        for (int play = 0; play < 10; play++)
        {
            SoundHandle handle = _builder.Audio.Play(sound);
            (float left, _) = _builder.Hear();
            heard.Add(left);
            _builder.Audio.Stop(handle, 0);
            _builder.Hear();
        }

        heard.Should().OnlyContain(value => value >= 0.249f && value <= 0.501f);
        heard.Distinct().Count().Should().BeGreaterThan(1);
    }

    [Fact]
    public void StartsAnEmittersSoundWithItsEntityAndStopsItWhenItGoes()
    {
        string sound = _builder.WithSound("Hum", "\"spatial\": false, \"loop\": true", 0.5f);
        Entity lamp = _builder.Spawn($$"""{ "name": "Lamp", "components": { "transform": {}, "soundEmitter": { "sound": "{{sound}}" } } }""");

        (float playing, _) = _builder.Hear();
        _builder.Game.Lifecycle.Destroy(_builder.Game.World, lamp);
        (float stopped, _) = _builder.Hear(48000 / 4);

        playing.Should().BeApproximately(0.5f, 0.001f);
        stopped.Should().Be(0);
    }

    [Fact]
    public void PlaysTheSoundsCommandsAskFor()
    {
        string sound = _builder.WithSound("Chime", Flat, 0.5f);

        PlaySoundCommand command = new(sound);
        _builder.Game.Events.Send(in command);
        _builder.Run();

        (float left, _) = _builder.Hear();
        left.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void DullsAndLowersTheAmbienceWhileTheListenerIsIndoors()
    {
        Entity listener = _builder.SpawnListener(10);
        _builder.Spawn("""{ "name": "Room", "components": { "transform": {}, "indoorVolume": { "size": [4, 3, 4] } } }""");
        string bed = _builder.WithSound("Street", "\"spatial\": false, \"loop\": true, \"bus\": \"Ambience\"", 0.5f);
        _builder.Audio.Play(bed);
        _builder.Run();
        (float outside, _) = _builder.Hear(48000);

        ref Transform transform = ref _builder.Game.World.Get<Transform>(listener);
        transform.Position = Vector3.Zero;
        _builder.Run();
        (float inside, _) = _builder.Hear(48000);

        _builder.Audio.IsIndoors.Should().BeTrue();
        outside.Should().BeApproximately(0.5f, 0.001f);
        inside.Should().BeApproximately(0.3f, 0.001f);
    }

    [Fact]
    public void HoldsTheWorldsSoundsButNotTheMusicWhileTheGameIsPaused()
    {
        string music = _builder.WithSound("Song", "\"spatial\": false, \"loop\": true, \"bus\": \"Music\"", 0.25f);
        string hum = _builder.WithSound("Hum", "\"spatial\": false, \"loop\": true", 0.5f);
        _builder.Audio.Play(music);
        _builder.Audio.Play(hum);
        Tag hub = new("Pause.Hub");

        _builder.Game.Pause(hub);
        _builder.Run();
        (float paused, _) = _builder.Hear();
        _builder.Game.Resume(hub);
        _builder.Run();
        (float resumed, _) = _builder.Hear();

        paused.Should().BeApproximately(0.25f, 0.001f);
        resumed.Should().BeApproximately(0.75f, 0.001f);
    }

    [Fact]
    public void FollowsTheVolumeSettings()
    {
        string sound = _builder.WithSound("Hum", "\"spatial\": false, \"loop\": true", 0.5f);
        _builder.Audio.Play(sound);

        _builder.Audio.Settings.Master.Value = 0.5f;
        _builder.Audio.Settings.Effects.Value = 0.5f;
        _builder.Run();

        (float left, _) = _builder.Hear();
        left.Should().BeApproximately(0.125f, 0.001f);
    }

    [Fact]
    public void PutsTheVolumesOnTheConsole()
    {
        string answer = _builder.Game.DebugConsole.Execute("audio.Music 0.25");

        answer.Should().NotBeNull();
        _builder.Audio.Settings.Music.Value.Should().Be(0.25f);
    }

    [Fact]
    public void GivesNoHandleForASoundItCannotPlay()
    {
        string noFile = _builder.WithSoundJson("Broken", """{ "type": "sound", "files": [ "Missing.wav" ] }""");
        string noFiles = _builder.WithSoundJson("Empty", """{ "type": "sound" }""");

        SoundHandle nowhere = _builder.Audio.Play("Nowhere.fvdata");
        SoundHandle broken = _builder.Audio.Play(noFile);
        SoundHandle empty = _builder.Audio.Play(noFiles);

        nowhere.IsNone.Should().BeTrue();
        broken.IsNone.Should().BeTrue();
        empty.IsNone.Should().BeTrue();
    }

    [Fact]
    public void KnowsWhenASoundHasEnded()
    {
        byte[] blip = WavFile.Constant(100, 0.5f);
        _builder.Write("Blip.wav", blip);
        string sound = _builder.WithSoundJson("Blip", """{ "type": "sound", "files": [ "Blip.wav" ], "spatial": false }""");

        SoundHandle handle = _builder.Audio.Play(sound);
        bool during = _builder.Audio.IsPlaying(handle);
        _builder.Hear();

        during.Should().BeTrue();
        _builder.Audio.IsPlaying(handle).Should().BeFalse();
    }

    [Fact]
    public void PlaysOggFiles()
    {
        string tone = Path.Combine(AppContext.BaseDirectory, "Sounds", "Tone.ogg");
        byte[] file = File.ReadAllBytes(tone);
        _builder.Write("Tone.ogg", file);
        string sound = _builder.WithSoundJson("Tone", """{ "type": "sound", "files": [ "Tone.ogg" ], "spatial": false }""");

        _builder.Audio.Play(sound);

        float[] heard = _builder.Device.Render(4800);
        heard.Max().Should().BeGreaterThan(0.05f);
    }
}
