using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Tests.Textures;

public sealed class DdsImageTests
{
    [Fact]
    public void ReadsWhatItWrites()
    {
        byte[] data = [.. Enumerable.Range(0, (9 + 2 + 1) * 16).Select(index => (byte)index)];
        DdsImage image = new(12, 9, PixelFormat.Bc7UNormSrgb, 3, data);
        using MemoryStream stream = new();

        image.Write(stream);
        stream.Position = 0;
        DdsImage read = DdsImage.Read(stream);

        read.Width.Should().Be(12);
        read.Height.Should().Be(9);
        read.Format.Should().Be(PixelFormat.Bc7UNormSrgb);
        read.MipLevels.Should().Be(3);
        read.Data.Should().Equal(data);
    }

    [Fact]
    public void DescribesATextureForItsData()
    {
        DdsImage image = new(2, 2, PixelFormat.Rgba8UNormSrgb, 2, new byte[20]);

        TextureDescription description = image.Describe(TextureUsage.ShaderRead);

        description.Should().Be(new TextureDescription(2, 2, PixelFormat.Rgba8UNormSrgb, TextureUsage.ShaderRead) { MipLevels = 2 });
    }

    [Fact]
    public void DataOfTheWrongSizeThrows()
    {
        Action create = () => _ = new DdsImage(4, 4, PixelFormat.Bc7UNorm, 1, new byte[15]);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AFileThatIsNotADdsThrows()
    {
        using MemoryStream stream = new([0x89, (byte)'P', (byte)'N', (byte)'G', 0, 0, 0, 0]);

        Action read = () => DdsImage.Read(stream);

        read.Should().Throw<InvalidDataException>();
    }
}
