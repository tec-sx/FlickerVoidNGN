namespace FlickerVoid.Engine.Shaders.Tests;

public sealed class DxcShaderCompilerTests : IDisposable
{
    private const string VertexSource = """
        float4 main(float3 position : POSITION) : SV_Position
        {
            return float4(position, 1);
        }
        """;

    private const string PixelSource = """
        Texture2D Albedo : register(t0);
        SamplerState Linear : register(s0);

        float4 main(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
        {
            return Albedo.Sample(Linear, uv);
        }
        """;

    private const string ComputeSource = """
        RWStructuredBuffer<uint> Values : register(u0);

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            Values[id.x] = id.x;
        }
        """;

    private readonly DxcShaderCompiler _compiler = new DxcShaderCompilerTestsBuilder().Build();

    public void Dispose() => _compiler.Dispose();

    [Fact]
    public void CompilesToSignedDxil()
    {
        ShaderSource source = new(VertexSource, "main", ShaderStage.Vertex);

        ShaderCompileResult result = _compiler.Compile(source, ShaderTarget.Dxil);

        result.Succeeded.Should().BeTrue(result.Messages);
        byte[] magic = result.Bytecode[..4];
        magic.Should().Equal("DXBC"u8.ToArray());

        // The container hash is all zero when DXC couldn't load dxil to sign it, and Direct3D 12 rejects unsigned shaders.
        byte[] hash = result.Bytecode[4..20];
        hash.Should().Contain(static b => b != 0);
    }

    [Fact]
    public void CompilesToSpirv()
    {
        ShaderSource source = new(VertexSource, "main", ShaderStage.Vertex);

        ShaderCompileResult result = _compiler.Compile(source, ShaderTarget.Spirv);

        result.Succeeded.Should().BeTrue(result.Messages);
        uint magic = BitConverter.ToUInt32(result.Bytecode, 0);
        magic.Should().Be(0x07230203u);
    }

    [Theory]
    [InlineData(ShaderTarget.Dxil)]
    [InlineData(ShaderTarget.Spirv)]
    public void CompilesEveryStage(ShaderTarget target)
    {
        ShaderSource pixel = new(PixelSource, "main", ShaderStage.Pixel);
        ShaderSource compute = new(ComputeSource, "main", ShaderStage.Compute);

        ShaderCompileResult pixelResult = _compiler.Compile(pixel, target);
        ShaderCompileResult computeResult = _compiler.Compile(compute, target);

        pixelResult.Succeeded.Should().BeTrue(pixelResult.Messages);
        computeResult.Succeeded.Should().BeTrue(computeResult.Messages);
    }

    [Fact]
    public void ReportsErrorsWithTheSourceName()
    {
        ShaderSource source = new("float4 main() : SV_Position { return missing; }", "main", ShaderStage.Vertex) { Name = "Broken.hlsl" };

        ShaderCompileResult result = _compiler.Compile(source, ShaderTarget.Spirv);

        result.Succeeded.Should().BeFalse();
        result.Bytecode.Should().BeEmpty();
        result.Messages.Should().Contain("Broken.hlsl").And.Contain("undeclared identifier 'missing'");
    }

    [Fact]
    public void FailsWhenTheEntryPointIsMissing()
    {
        ShaderSource source = new(VertexSource, "VSMain", ShaderStage.Vertex);

        ShaderCompileResult result = _compiler.Compile(source, ShaderTarget.Dxil);

        result.Succeeded.Should().BeFalse();
        result.Messages.Should().Contain("missing entry point");
    }

    [Fact]
    public void AppliesDefines()
    {
        const string source = """
            float4 main() : SV_Position
            {
            #ifndef OFFSET
                return missing;
            #else
                return OFFSET;
            #endif
            }
            """;
        ShaderSource defined = new(source, "main", ShaderStage.Vertex) { Defines = ["OFFSET=float4(1, 0, 0, 1)"] };

        ShaderCompileResult result = _compiler.Compile(defined, ShaderTarget.Spirv);

        result.Succeeded.Should().BeTrue(result.Messages);
    }
}
