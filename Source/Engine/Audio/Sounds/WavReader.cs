using System.Buffers.Binary;
using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Decodes WAV files: PCM of 8, 16, 24 or 32 bits and 32-bit floats, mono or stereo (the first two channels of more).</summary>
public static class WavReader
{
    private const ushort Pcm = 1;
    private const ushort Float = 3;
    private const ushort Extensible = 0xFFFE;

    public static SoundClip Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 12 || !file[..4].SequenceEqual("RIFF"u8) || !file.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a WAV file.");
        }

        ushort format = 0;
        int channels = 0;
        int sampleRate = 0;
        int bits = 0;
        int position = 12;
        while (position + 8 <= file.Length)
        {
            ReadOnlySpan<byte> id = file.Slice(position, 4);
            uint declared = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(position + 4, 4));
            int start = position + 8;
            long left = file.Length - start;
            int size = (int)Math.Min(declared, left);
            ReadOnlySpan<byte> chunk = file.Slice(start, size);
            if (id.SequenceEqual("fmt "u8) && size >= 16)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);
                if (format == Extensible && size >= 26)
                {
                    format = BinaryPrimitives.ReadUInt16LittleEndian(chunk[24..]);
                }
            }
            else if (id.SequenceEqual("data"u8))
            {
                return Decode(chunk, format, channels, sampleRate, bits);
            }

            position = start + size + (size & 1);
        }

        throw new InvalidDataException("The WAV file has no data.");
    }

    private static SoundClip Decode(ReadOnlySpan<byte> data, ushort format, int channels, int sampleRate, int bits)
    {
        bool known = (format == Pcm && bits is 8 or 16 or 24 or 32) || (format == Float && bits == 32);
        if (!known || channels < 1 || sampleRate <= 0)
        {
            throw new InvalidDataException($"Unsupported WAV format: format {format}, {bits} bits, {channels} channels.");
        }

        int bytes = bits / 8;
        int frames = data.Length / (bytes * channels);
        int kept = Math.Min(channels, 2);
        float[] samples = new float[frames * kept];
        for (int frame = 0; frame < frames; frame++)
        {
            for (int channel = 0; channel < kept; channel++)
            {
                int offset = ((frame * channels) + channel) * bytes;
                ReadOnlySpan<byte> sample = data.Slice(offset, bytes);
                samples[(frame * kept) + channel] = ReadSample(sample, format, bits);
            }
        }

        return new SoundClip(samples, kept, sampleRate);
    }

    private static float ReadSample(ReadOnlySpan<byte> sample, ushort format, int bits)
    {
        if (format == Float)
        {
            float value = BinaryPrimitives.ReadSingleLittleEndian(sample);
            return Math.Clamp(value, -1, 1);
        }

        switch (bits)
        {
            case 8:
                return (sample[0] - 128) / 128f;
            case 16:
                return BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f;
            case 24:
                int whole = sample[0] | (sample[1] << 8) | ((sbyte)sample[2] << 16);
                return whole / 8388608f;
            default:
                return BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648f;
        }
    }
}
