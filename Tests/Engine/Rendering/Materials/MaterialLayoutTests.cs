using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Engine.Rendering.Tests.Materials;

public sealed class MaterialLayoutTests
{
    private readonly MaterialLayoutTestsBuilder _builder = new();

    [Fact]
    public void PacksTheDefaultsTightlyInTheDeclaredOrder()
    {
        byte[] packed = MaterialLayout.Pack(_builder.Parameters, null);

        float[] values = MemoryMarshal.Cast<byte, float>(packed).ToArray();
        values.Should().Equal(0.25f, 0, 0, 0, BitConverter.UInt32BitsToSingle(MaterialLayout.NoTexture));
    }

    [Fact]
    public void PacksTheMaterialsValuesOverTheDefaults()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "emissive": [1, 0.5, 0] } }""");

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, material);

        float[] values = MemoryMarshal.Cast<byte, float>(packed).ToArray();
        values.Should().Equal(0.25f, 1, 0.5f, 0, BitConverter.UInt32BitsToSingle(MaterialLayout.NoTexture));
    }

    [Fact]
    public void PacksATextureAsTheIndexItLoadsAs()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "texture": "Paintings/Wall.reb" } }""");
        List<string> loaded = [];

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, material, path =>
        {
            loaded.Add(path);
            return 7;
        });

        uint[] values = MemoryMarshal.Cast<byte, uint>(packed).ToArray();
        values[^1].Should().Be(7);
        loaded.Should().Equal("Paintings/Wall.reb");
    }

    [Fact]
    public void PacksNoTextureForATextureSetToNull()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "texture": null } }""");

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, material, _ => 7);

        uint[] values = MemoryMarshal.Cast<byte, uint>(packed).ToArray();
        values[^1].Should().Be(MaterialLayout.NoTexture);
    }

    [Fact]
    public void RejectsAParameterTheShaderDoesNotDeclare()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "glow": 1 } }""");

        Action pack = () => MaterialLayout.Pack(_builder.Parameters, material);

        pack.Should().Throw<InvalidDataException>().WithMessage("*glow*");
    }

    [Fact]
    public void RejectsAValueOfTheWrongSize()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "emissive": 1 } }""");

        Action pack = () => MaterialLayout.Pack(_builder.Parameters, material);

        pack.Should().Throw<InvalidDataException>().WithMessage("*emissive*3*");
    }
}
