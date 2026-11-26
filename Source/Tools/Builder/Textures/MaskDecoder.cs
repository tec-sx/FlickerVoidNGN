using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Textures;

using StbImageSharp;

namespace FlickerVoid.Tools.Builder.Textures;

/// <summary>
/// Reads an image as a mask: every channel as 16 bits (an 8-bit image widened), kept linear in half floats, one level. A grey
/// image (with or without alpha) fills only red; the rest are 0 but alpha.
/// </summary>
internal static class MaskDecoder
{
    private const int Channels = 4;
    private const int GreyChannels = 2;

    public static unsafe DdsImage Decode(byte[] encoded)
    {
        using MemoryStream stream = new(encoded);
        StbImage.stbi__context context = new(stream);
        int width;
        int height;
        int components;
        ushort* texels = (ushort*)StbImage.stbi__load_and_postprocess_16bit(context, &width, &height, &components, Channels);
        if (texels is null)
        {
            throw new InvalidDataException($"The mask could not be read: {StbImage.stbi__g_failure_reason}.");
        }

        try
        {
            int count = width * height * Channels;
            Half[] halves = new Half[count];
            bool grey = components <= GreyChannels;
            for (int texel = 0; texel < count; texel += Channels)
            {
                for (int channel = 0; channel < Channels; channel++)
                {
                    float value = texels[texel + channel] / (float)ushort.MaxValue;
                    if (grey && channel is 1 or 2)
                    {
                        value = 0;
                    }

                    halves[texel + channel] = (Half)value;
                }
            }

            byte[] data = MemoryMarshal.AsBytes(halves.AsSpan()).ToArray();
            return new DdsImage(width, height, PixelFormat.Rgba16Float, 1, data);
        }
        finally
        {
            // StbImageSharp allocates its results with Marshal.AllocHGlobal.
            Marshal.FreeHGlobal((nint)texels);
        }
    }
}
