using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Engine.Builder.Tests.Shaders;

public sealed class ShaderImporterTests : IDisposable
{
    private const uint SpirvMagic = 0x07230203u;

    private const string Colors = """
        float4 Tint() { return float4(1, 0.5, 0, 1); }
        """;

    private const string Simple = """
        #include "Common/Colors.hlsli"

        float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
        {
            return float4(vertexId, 0, 0, 1);
        }

        float4 PixelMain() : SV_Target
        {
            return Tint();
        }
        """;

    private const string SimpleManifest = """
        {
          // Each entry point compiles to Simple.<entry point>.spv.
          "entryPoints": { "VertexMain": "Vertex", "PixelMain": "Pixel" }
        }
        """;

    private readonly ShaderImporterTestsBuilder _files = new();

    public void Dispose()
    {
        _files.Dispose();
    }

    [Fact]
    public void CompilesEachEntryPointToSpirvAndLeavesTheHlslOut()
    {
        _files.WithSource("Shaders/Simple.fvshader", SimpleManifest)
            .WithSource("Shaders/Simple.hlsl", Simple)
            .WithSource("Shaders/Common/Colors.hlsli", Colors);
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        byte[] vertex = _files.ReadOutput("Shaders/Simple.VertexMain.spv");
        byte[] pixel = _files.ReadOutput("Shaders/Simple.PixelMain.spv");
        BitConverter.ToUInt32(vertex, 0).Should().Be(SpirvMagic);
        BitConverter.ToUInt32(pixel, 0).Should().Be(SpirvMagic);
        _files.OutputExists("Shaders/Simple.hlsl").Should().BeFalse();
        _files.OutputExists("Shaders/Common/Colors.hlsli").Should().BeFalse();
    }

    [Fact]
    public void RebuildsWhenAnIncludedFileChanges()
    {
        _files.WithSource("Shaders/Simple.fvshader", SimpleManifest)
            .WithSource("Shaders/Simple.hlsl", Simple)
            .WithSource("Shaders/Common/Colors.hlsli", Colors);
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.WriteSource("Shaders/Common/Colors.hlsli", "float4 Tint() { return 1; }");

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Contain("Shaders/Simple.fvshader");
    }

    [Fact]
    public void ACompileErrorFailsWithTheCompilerMessage()
    {
        _files.WithSource("Broken.fvshader", """{ "entryPoints": { "PixelMain": "Pixel" } }""")
            .WithSource("Broken.hlsl", "float4 PixelMain() : SV_Target { return undefinedValue; }");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Message.Should().Contain("undefinedValue");
    }

    [Fact]
    public void WritesTheMaterialParametersAShaderDeclaresBesideIt()
    {
        _files.WithSource("Shaders/Lit.fvshader", """
                {
                  "entryPoints": { "PixelMain": "Pixel" },
                  "parameters": [
                    { "name": "wrap", "type": "float", "default": 0, "minimum": 0, "maximum": 1 },
                    { "name": "emissive", "type": "color", "default": [0, 0, 0] }
                  ]
                }
                """)
            .WithSource("Shaders/Lit.hlsl", "float4 PixelMain() : SV_Target { return 1; }");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        byte[] json = _files.ReadOutput("Shaders/Lit.fvparams");
        ShaderParameters? parameters = JsonSerializer.Deserialize(json, RenderingJsonContext.Default.ShaderParameters);
        string[] names = [.. parameters!.Parameters.Select(parameter => parameter.Name)];
        names.Should().Equal("wrap", "emissive");
        parameters.Parameters[0].Maximum.Should().Be(1);
        parameters.Parameters[1].Type.Should().Be(ShaderParameterType.Color);
    }

    [Fact]
    public void WritesNoParametersForAShaderThatDeclaresNone()
    {
        _files.WithSource("Shaders/Simple.fvshader", SimpleManifest)
            .WithSource("Shaders/Simple.hlsl", Simple)
            .WithSource("Shaders/Common/Colors.hlsli", Colors);
        AssetBuilder builder = _files.Build();

        _files.BuildAll(builder);

        _files.OutputExists("Shaders/Simple.fvparams").Should().BeFalse();
    }
}
