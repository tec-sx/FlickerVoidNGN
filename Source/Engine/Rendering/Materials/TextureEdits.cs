using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>RGBA texels, rows from the top, and the edits a texture's settings make to them: crop, shrink and tint.</summary>
public readonly record struct TextureEdits(byte[] Rgba, int Width, int Height)
{
    /// <summary>The texels after every edit the settings ask for, cropped first, then shrunk, then tinted.</summary>
    public TextureEdits Apply(TextureSettings settings)
    {
        TextureEdits cropped = Crop(settings.CropX, settings.CropY, settings.CropWidth, settings.CropHeight);
        TextureEdits shrunk = cropped.ShrinkTo(settings.MaxSize);
        return shrunk.Tint(settings.Tint);
    }

    /// <summary>The part of the texels inside the rectangle, clamped to the image; a width or height of 0 reaches its edge.</summary>
    public TextureEdits Crop(int x, int y, int width, int height)
    {
        int left = Math.Clamp(x, 0, Width - 1);
        int top = Math.Clamp(y, 0, Height - 1);
        int right = Width;
        if (width > 0)
        {
            right = Math.Min(left + width, Width);
        }

        int bottom = Height;
        if (height > 0)
        {
            bottom = Math.Min(top + height, Height);
        }

        int croppedWidth = right - left;
        int croppedHeight = bottom - top;
        if (croppedWidth == Width && croppedHeight == Height)
        {
            return this;
        }

        byte[] rgba = new byte[croppedWidth * croppedHeight * 4];
        for (int row = 0; row < croppedHeight; row++)
        {
            int source = ((top + row) * Width + left) * 4;
            Array.Copy(Rgba, source, rgba, row * croppedWidth * 4, croppedWidth * 4);
        }

        return new TextureEdits(rgba, croppedWidth, croppedHeight);
    }

    /// <summary>The texels halved until neither side is larger than the limit; 0 keeps them.</summary>
    public TextureEdits ShrinkTo(int maxSize)
    {
        TextureEdits edits = this;
        while (maxSize > 0 && (edits.Width > maxSize || edits.Height > maxSize) && (edits.Width > 1 || edits.Height > 1))
        {
            edits = edits.Halve();
        }

        return edits;
    }

    /// <summary>The texels at half the size, each the average of the two by two it covers.</summary>
    public TextureEdits Halve()
    {
        int width = Math.Max(Width / 2, 1);
        int height = Math.Max(Height / 2, 1);
        byte[] rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int x0 = Math.Min(x * 2, Width - 1);
                int x1 = Math.Min(x * 2 + 1, Width - 1);
                int y0 = Math.Min(y * 2, Height - 1);
                int y1 = Math.Min(y * 2 + 1, Height - 1);
                for (int channel = 0; channel < 4; channel++)
                {
                    int sum = Rgba[(y0 * Width + x0) * 4 + channel] + Rgba[(y0 * Width + x1) * 4 + channel]
                        + Rgba[(y1 * Width + x0) * 4 + channel] + Rgba[(y1 * Width + x1) * 4 + channel];
                    rgba[(y * width + x) * 4 + channel] = (byte)((sum + 2) / 4);
                }
            }
        }

        return new TextureEdits(rgba, width, height);
    }

    /// <summary>The texels with their colour multiplied by the tint.</summary>
    public TextureEdits Tint(Vector3 tint)
    {
        if (tint == Vector3.One)
        {
            return this;
        }

        byte[] rgba = new byte[Rgba.Length];
        for (int texel = 0; texel < Rgba.Length; texel += 4)
        {
            rgba[texel] = Scale(Rgba[texel], tint.X);
            rgba[texel + 1] = Scale(Rgba[texel + 1], tint.Y);
            rgba[texel + 2] = Scale(Rgba[texel + 2], tint.Z);
            rgba[texel + 3] = Rgba[texel + 3];
        }

        return new TextureEdits(rgba, Width, Height);
    }

    /// <summary>The texels with every mip level after them, down to one texel.</summary>
    public byte[] WithMips(out int levels)
    {
        List<byte> data = [.. Rgba];
        levels = 1;
        TextureEdits level = this;
        while (level.Width > 1 || level.Height > 1)
        {
            level = level.Halve();
            data.AddRange(level.Rgba);
            levels++;
        }

        return [.. data];
    }

    private static byte Scale(byte value, float factor)
    {
        float scaled = MathF.Round(value * factor);
        return (byte)Math.Clamp(scaled, 0, 255);
    }
}
