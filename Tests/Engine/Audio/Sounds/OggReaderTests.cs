using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Sounds;

namespace FlickerVoid.Engine.Audio.Tests.Sounds;

public sealed class OggReaderTests
{
    [Fact]
    public void DecodesAWholeFile()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Sounds", "Tone.ogg");
        using FileStream file = File.OpenRead(path);

        SoundClip clip = OggReader.Read(file);

        clip.Channels.Should().Be(2);
        clip.SampleRate.Should().Be(44100);
        clip.Seconds.Should().BeApproximately(0.25, 0.01);
        clip.Samples.Max().Should().BeGreaterThan(0.05f);
    }
}
