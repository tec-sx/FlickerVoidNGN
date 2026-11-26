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
        values.Should().Equal(0.25f, 0, 0, 0, BitConverter.UInt32BitsToSingle(MaterialLayout.NoTexture), BitConverter.UInt32BitsToSingle(MaterialLayout.NoTexture));
    }

    [Fact]
    public void PacksTheMaterialsValuesOverTheDefaults()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "emissive": [1, 0.5, 0] } }""");

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, material);

        float[] values = MemoryMarshal.Cast<byte, float>(packed).ToArray();
        values.Should().Equal(0.25f, 1, 0.5f, 0, BitConverter.UInt32BitsToSingle(MaterialLayout.NoTexture), BitConverter.UInt32BitsToSingle(MaterialLayout.NoTexture));
    }

    [Fact]
    public void PacksATextureAsTheIndicesOfItsImageAndRelief()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "texture": "Paintings/Wall.reb" } }""");
        List<string> loaded = [];

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, material, path =>
        {
            loaded.Add(path);
            return new MaterialTexture(7, 9);
        });

        uint[] values = MemoryMarshal.Cast<byte, uint>(packed).ToArray();
        values[^2..].Should().Equal(7u, 9u);
        loaded.Should().Equal("Paintings/Wall.reb");
    }

    [Fact]
    public void PacksNoTextureForATextureSetToNull()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "texture": null } }""");

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, material, _ => new MaterialTexture(7, 9));

        uint[] values = MemoryMarshal.Cast<byte, uint>(packed).ToArray();
        values[^2..].Should().Equal(MaterialLayout.NoTexture, MaterialLayout.NoTexture);
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

    [Fact]
    public void SizesTheParametersAsTheyPack()
    {
        int size = MaterialLayout.Size(_builder.Parameters);

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, null);
        size.Should().Be(packed.Length).And.Be(24);
    }

    [Fact]
    public void PacksAnInstanceAsTheSceneShadersThenItsMastersModelThenTheMastersOwn()
    {
        MasterFile master = MaterialLayoutTestsBuilder.Master("""
            { "shadingModel": "Metal", "parameters": [{ "name": "rust", "type": "float", "default": 0 }, { "name": "metalness", "type": "float", "default": 1 }] }
            """);
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "metalness": 0.5, "rust": 0.25 } }""");

        byte[] packed = MaterialLayout.PackInstance(_builder.Parameters, master, material);

        float[] values = MemoryMarshal.Cast<byte, float>(packed).ToArray();
        int model = MaterialLayout.Size(_builder.Parameters) / sizeof(float);
        BitConverter.SingleToUInt32Bits(values[model]).Should().Be((uint)ShadingModel.Metal);
        values[(model + 1)..(model + 5)].Should().Equal(0.5f, 0, 0, 0);
        values[(model + 5)..].Should().Equal(0.25f, 0.5f);
        packed.Length.Should().Be(MaterialLayout.MasterOffset(_builder.Parameters) + 8);
    }

    [Fact]
    public void PacksSkinsScatterColorThenItsWidthAsTheModelsValues()
    {
        MasterFile master = MaterialLayoutTestsBuilder.Master("""
            { "shadingModel": "Skin", "parameters": [{ "name": "scatterWidth", "type": "float", "default": 0.5 }, { "name": "scatter", "type": "color", "default": [1, 0, 0] }] }
            """);
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "scatter": [0.9, 0.3, 0.2] } }""");

        byte[] packed = MaterialLayout.PackInstance(_builder.Parameters, master, material);

        float[] values = MemoryMarshal.Cast<byte, float>(packed).ToArray();
        int model = MaterialLayout.Size(_builder.Parameters) / sizeof(float);
        values[(model + 1)..(model + 5)].Should().Equal(0.9f, 0.3f, 0.2f, 0.5f);
    }

    [Fact]
    public void PacksAMaterialWithoutAMasterAsPainted()
    {
        byte[] packed = MaterialLayout.PackInstance(_builder.Parameters, null, null);

        packed.Length.Should().Be(MaterialLayout.MasterOffset(_builder.Parameters));
        uint model = BitConverter.ToUInt32(packed, MaterialLayout.Size(_builder.Parameters));
        model.Should().Be((uint)ShadingModel.Painted);
    }

    [Fact]
    public void RefusesANameNeitherTheShaderNorTheMasterDeclares()
    {
        MasterFile master = MaterialLayoutTestsBuilder.Master("""{ "parameters": [{ "name": "rust", "type": "float", "default": 0 }] }""");
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "mortar": 1 } }""");

        Action pack = () => MaterialLayout.PackInstance(_builder.Parameters, master, material);

        pack.Should().Throw<InvalidDataException>().WithMessage("*mortar*");
    }
}
