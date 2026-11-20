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
        values.Should().Equal(0.25f, 0, 0, 0);
    }

    [Fact]
    public void PacksTheMaterialsValuesOverTheDefaults()
    {
        MaterialFile material = MaterialLayoutTestsBuilder.Material("""{ "parameters": { "emissive": [1, 0.5, 0] } }""");

        byte[] packed = MaterialLayout.Pack(_builder.Parameters, material);

        float[] values = MemoryMarshal.Cast<byte, float>(packed).ToArray();
        values.Should().Equal(0.25f, 1, 0.5f, 0);
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
