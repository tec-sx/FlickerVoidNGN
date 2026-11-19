namespace FlickerVoid.Engine.Graphics.Textures;

/// <summary>Size, format and use of a 2D texture.</summary>
public readonly record struct TextureDescription(int Width, int Height, PixelFormat Format, TextureUsage Usage)
{
    /// <summary>Levels in the mip chain, the full size first; <see cref="CountMipLevels"/> gives a full chain.</summary>
    public int MipLevels { get; init; } = 1;

    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }

    /// <summary>The levels of a full mip chain, halving down to 1x1.</summary>
    public static int CountMipLevels(int width, int height)
    {
        int largest = Math.Max(width, height);
        return (int)Math.Log2(largest) + 1;
    }

    /// <summary>Bytes in one mip level's tightly packed rows.</summary>
    public int GetLevelSize(int mipLevel)
    {
        TextureDescription level = GetMipDescription(mipLevel);
        int rowSize = Format.GetRowSize(level.Width);
        int rowCount = Format.GetRowCount(level.Height);
        return rowSize * rowCount;
    }

    /// <summary>Bytes in every mip level's tightly packed rows, the full size first.</summary>
    public int GetDataSize()
    {
        int size = 0;
        for (int level = 0; level < MipLevels; level++)
        {
            size += GetLevelSize(level);
        }

        return size;
    }

    /// <summary>This description sized to one mip level, with only that level.</summary>
    public TextureDescription GetMipDescription(int mipLevel)
    {
        int width = Math.Max(1, Width >> mipLevel);
        int height = Math.Max(1, Height >> mipLevel);
        return this with { Width = width, Height = height, MipLevels = 1 };
    }
}

