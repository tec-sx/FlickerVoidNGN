using System.Diagnostics;

using FlickerVoid.Engine.Audio.Devices;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Tests.Mixing;

namespace FlickerVoid.Engine.Audio.Tests.Devices;

public sealed class SdlAudioDeviceTests
{
    [Fact]
    public void PullsTheMixerFromSdlsAudioThread()
    {
        Mixer mixer = new();
        SoundClip clip = MixerTestsBuilder.Constant(480);
        ClipSource source = new(clip);
        MixerCommand play = MixerCommand.Play(1, source, AudioBus.Effects, 1, 1);
        mixer.Send(in play);

        // SDL's dummy driver plays to nowhere at the real pace, so this runs where there are no speakers.
        using SdlAudioDevice? device = SdlAudioDevice.TryOpen(mixer, out string problem, "dummy");
        Assert.SkipWhen(device is null, $"SDL opened no audio device: {problem}");
        Stopwatch waited = Stopwatch.StartNew();
        bool finished = false;
        while (!finished && waited.Elapsed < TimeSpan.FromSeconds(5))
        {
            Thread.Sleep(10);
            finished = mixer.TryTakeFinished(out _);
        }

        finished.Should().BeTrue();
        mixer.Position.Should().BeGreaterThanOrEqualTo(480);
    }

    [Fact]
    public void OpensWithTheBufferAskedFor()
    {
        Mixer mixer = new();

        using SdlAudioDevice? device = SdlAudioDevice.TryOpen(mixer, out string problem, "dummy", 4096);
        Assert.SkipWhen(device is null, $"SDL opened no audio device: {problem}");

        device!.BufferFrames.Should().Be(4096);
    }
}
