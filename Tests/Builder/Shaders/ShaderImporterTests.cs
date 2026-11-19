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
        _files.WithSource("Shaders/Simple.shader.json", SimpleManifest)
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
        _files.WithSource("Shaders/Simple.shader.json", SimpleManifest)
            .WithSource("Shaders/Simple.hlsl", Simple)
            .WithSource("Shaders/Common/Colors.hlsli", Colors);
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.WriteSource("Shaders/Common/Colors.hlsli", "float4 Tint() { return 1; }");

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Contain("Shaders/Simple.shader.json");
    }

    [Fact]
    public void ACompileErrorFailsWithTheCompilerMessage()
    {
        _files.WithSource("Broken.shader.json", """{ "entryPoints": { "PixelMain": "Pixel" } }""")
            .WithSource("Broken.hlsl", "float4 PixelMain() : SV_Target { return undefinedValue; }");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Message.Should().Contain("undefinedValue");
    }
}
