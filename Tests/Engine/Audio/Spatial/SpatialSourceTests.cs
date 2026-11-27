using System.Numerics;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Spatial;

namespace FlickerVoid.Engine.Audio.Tests.Spatial;

public sealed class SpatialSourceTests : IDisposable
{
    private readonly SteamAudio? _audio = SteamAudio.TryCreate();

    public void Dispose() => _audio?.Dispose();

    private static SoundClip Noise(int frames)
    {
        Random random = new(7);
        float[] samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            samples[frame] = (random.NextSingle() * 2) - 1;
        }

        return new SoundClip(samples, 1, AudioFormat.SampleRate);
    }

    private (double Left, double Right) Hear(Vector3 direction)
    {
        using SpatialSource source = new(new ClipSource(Noise(48000)), _audio!) { Direction = direction };
        float[] block = new float[512 * 2];
        double left = 0;
        double right = 0;
        for (int read = 0; read < 20; read++)
        {
            int frames = source.Read(block);
            for (int frame = 0; frame < frames; frame++)
            {
                left += Math.Abs(block[frame * 2]);
                right += Math.Abs(block[(frame * 2) + 1]);
            }
        }

        return (left, right);
    }

    [Fact]
    public void HearsASoundToTheRightLouderInTheRightEar()
    {
        Assert.SkipUnless(_audio is not null, "Steam Audio's library is not built here.");

        (double left, double right) = Hear(new Vector3(1, 0, 0));

        right.Should().BeGreaterThan(left * 1.5);
    }

    [Fact]
    public void HearsASoundToTheLeftLouderInTheLeftEar()
    {
        Assert.SkipUnless(_audio is not null, "Steam Audio's library is not built here.");

        (double left, double right) = Hear(new Vector3(-1, 0, 0));

        left.Should().BeGreaterThan(right * 1.5);
    }

    [Fact]
    public void ScalesTheSoundByItsGainAndEndsWithItsSource()
    {
        Assert.SkipUnless(_audio is not null, "Steam Audio's library is not built here.");
        using SpatialSource source = new(new ClipSource(Noise(1000)), _audio!) { Direction = new Vector3(0, 0, -1), Gain = 0 };
        float[] block = new float[2048 * 2];

        int frames = source.Read(block);

        frames.Should().Be(1000);
        block.Take(frames * 2).Should().OnlyContain(sample => Math.Abs(sample) < 1e-6f);
    }

    [Fact]
    public void TurnsTheDirectionOfASourceIntoTheListenersAxes()
    {
        Vector3 heard = SteamAudio.ToListenerSpace(new Vector3(0, 0, -5), Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ);

        heard.Should().Be(new Vector3(0, 0, -1));
    }
}
