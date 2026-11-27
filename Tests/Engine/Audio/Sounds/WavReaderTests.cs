using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Sounds;

namespace FlickerVoid.Engine.Audio.Tests.Sounds;

public sealed class WavReaderTests
{
    [Fact]
    public void ReadsSixteenBitStereo()
    {
        byte[] file = WavFile.Pcm16(2, 44100, 0.5f, -0.5f, 0.25f, -0.25f);

        SoundClip clip = WavReader.Read(file);

        clip.Channels.Should().Be(2);
        clip.SampleRate.Should().Be(44100);
        clip.Frames.Should().Be(2);
        clip.Samples[0].Should().BeApproximately(0.5f, 0.001f);
        clip.Samples[3].Should().BeApproximately(-0.25f, 0.001f);
    }

    [Fact]
    public void ReadsFloats()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        WavFile.WriteHeader(writer, 3, 1, 48000, 32, 8);
        writer.Write(0.75f);
        writer.Write(-0.125f);
        writer.Flush();

        byte[] file = stream.ToArray();

        SoundClip clip = WavReader.Read(file);

        clip.Samples.Should().Equal(0.75f, -0.125f);
    }

    [Fact]
    public void ReadsEightAndTwentyFourBits()
    {
        using MemoryStream eight = new();
        using BinaryWriter eightWriter = new(eight);
        WavFile.WriteHeader(eightWriter, 1, 1, 8000, 8, 2);
        eightWriter.Write((byte)128);
        eightWriter.Write((byte)192);
        eightWriter.Flush();
        using MemoryStream twentyFour = new();
        using BinaryWriter twentyFourWriter = new(twentyFour);
        WavFile.WriteHeader(twentyFourWriter, 1, 1, 48000, 24, 3);
        twentyFourWriter.Write([0x00, 0x00, 0xC0]);
        twentyFourWriter.Flush();

        byte[] eightBits = eight.ToArray();
        byte[] twentyFourBits = twentyFour.ToArray();

        SoundClip low = WavReader.Read(eightBits);
        SoundClip high = WavReader.Read(twentyFourBits);

        low.Samples.Should().Equal(0, 0.5f);
        high.Samples.Should().Equal(-0.5f);
    }

    [Fact]
    public void SkipsChunksItDoesNotKnow()
    {
        byte[] plain = WavFile.Pcm16(1, 48000, 0.5f);
        List<byte> file = [.. plain[..12]];
        file.AddRange([(byte)'L', (byte)'I', (byte)'S', (byte)'T', 3, 0, 0, 0, 1, 2, 3, 0]);
        file.AddRange(plain[12..]);

        SoundClip clip = WavReader.Read([.. file]);

        clip.Frames.Should().Be(1);
    }

    [Fact]
    public void RefusesWhatIsNotAWavFile()
    {
        byte[] file = "OggS and then some more bytes"u8.ToArray();

        Action read = () => WavReader.Read(file);

        read.Should().Throw<InvalidDataException>();
    }
}
