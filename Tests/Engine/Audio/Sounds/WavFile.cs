namespace FlickerVoid.Engine.Audio.Tests.Sounds;

/// <summary>Writes WAV files for the tests.</summary>
internal static class WavFile
{
    /// <summary>A 16-bit PCM file holding the samples (from -1 to 1), interleaved by channel.</summary>
    public static byte[] Pcm16(int channels, int sampleRate, params float[] samples)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        int dataSize = samples.Length * 2;
        WriteHeader(writer, 1, channels, sampleRate, 16, dataSize);
        foreach (float sample in samples)
        {
            float scaled = MathF.Round(sample * 32767);
            writer.Write((short)scaled);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>A mono 16-bit file at 48 kHz holding one value throughout.</summary>
    public static byte[] Constant(int frames, float value)
    {
        float[] samples = new float[frames];
        Array.Fill(samples, value);
        return Pcm16(1, 48000, samples);
    }

    public static void WriteHeader(BinaryWriter writer, ushort format, int channels, int sampleRate, int bits, int dataSize)
    {
        int blockAlign = channels * bits / 8;
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write(format);
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)bits);
        writer.Write("data"u8);
        writer.Write(dataSize);
    }
}
