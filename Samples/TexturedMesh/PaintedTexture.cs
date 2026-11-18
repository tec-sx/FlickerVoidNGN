namespace FlickerVoid.Samples.TexturedMesh;

/// <summary>A small stand-in for a hand-painted texture: two warm tones in a checker pattern with a dark frame.</summary>
public static class PaintedTexture
{
    public const int Size = 64;

    private const int Squares = 4;
    private const int Frame = 3;

    private static readonly byte[] Light = [226, 178, 120, 255];
    private static readonly byte[] Dark = [158, 92, 64, 255];
    private static readonly byte[] Outline = [52, 34, 30, 255];

    /// <summary>Rgba8UNorm texels, row by row from the top.</summary>
    public static byte[] Create()
    {
        byte[] texels = new byte[Size * Size * 4];
        int square = Size / Squares;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                byte[] color = PickColor(x, y, square);
                color.CopyTo(texels, (y * Size + x) * 4);
            }
        }

        return texels;
    }

    private static byte[] PickColor(int x, int y, int square)
    {
        bool onFrame = x < Frame || y < Frame || x >= Size - Frame || y >= Size - Frame;
        if (onFrame)
        {
            return Outline;
        }

        bool even = (x / square + y / square) % 2 == 0;
        if (even)
        {
            return Light;
        }

        return Dark;
    }
}
