namespace FlickerVoid.Tools.Builder.Tests.Shaders;

public sealed class MasterImporterTests : IDisposable
{
    private const uint SpirvMagic = 0x07230203u;

    private const string Master = """
        {
          // A master with one color of its own.
          "shadingModel": "Painted",
          "parameters": [{ "name": "grime", "type": "color", "default": [0, 0, 0] }]
        }
        """;

    private const string Surface = """
        #include "Grime.hlsli"

        Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
        {
            Surface surface = ReadSurface(input, draw, material);
            surface.Albedo.rgb = Dirty(surface.Albedo.rgb, parameters.Grime);
            return surface;
        }
        """;

    private const string Grime = """
        float3 Dirty(float3 color, float3 grime) { return lerp(color, grime, 0.5); }
        """;

    private readonly MasterImporterTestsBuilder _files = new();

    public void Dispose()
    {
        _files.Dispose();
    }

    [Fact]
    public void CompilesTheMastersGeometryPassAndCopiesItsFile()
    {
        _files.WithSource("Masters/Brick.fvmaster", Master)
            .WithSource("Masters/Brick.hlsl", Surface)
            .WithSource("Masters/Grime.hlsli", Grime);
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        byte[] geometry = _files.ReadOutput("Masters/Brick.GeometryMain.spv");
        BitConverter.ToUInt32(geometry, 0).Should().Be(SpirvMagic);
        _files.ReadOutputText("Masters/Brick.fvmaster").Should().Be(Master);
        _files.OutputExists("Masters/Brick.hlsl").Should().BeFalse();
    }

    [Fact]
    public void BuildsTheMasterAgainWhenItsHlslOrAnIncludeChanges()
    {
        _files.WithSource("Masters/Brick.fvmaster", Master)
            .WithSource("Masters/Brick.hlsl", Surface)
            .WithSource("Masters/Grime.hlsli", Grime);
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);

        _files.WriteSource("Masters/Grime.hlsli", "float3 Dirty(float3 color, float3 grime) { return color * grime; }");
        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Contain("Masters/Brick.fvmaster");
    }

    [Fact]
    public void BuildsTheMasterAgainWhenTheEngineShadersItIsWrappedInChange()
    {
        _files.WithEngineCopy()
            .WithSource("Masters/Brick.fvmaster", Master)
            .WithSource("Masters/Brick.hlsl", Surface)
            .WithSource("Masters/Grime.hlsli", Grime);
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);

        _files.AppendEngineShader("Geometry.hlsl", "\n// Changed.\n");
        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Contain("Masters/Brick.fvmaster");
    }

    [Fact]
    public void FailsAMasterWhoseSurfaceDoesNotCompile()
    {
        _files.WithSource("Masters/Brick.fvmaster", Master)
            .WithSource("Masters/Brick.hlsl", "Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters) { return nothing; }");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Message.Should().Contain("nothing");
    }

    [Fact]
    public void FailsAMasterWithoutItsHlsl()
    {
        _files.WithSource("Masters/Brick.fvmaster", Master);
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Message.Should().Contain("Brick.hlsl");
    }
}
