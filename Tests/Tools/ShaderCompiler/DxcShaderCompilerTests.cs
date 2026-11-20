namespace FlickerVoid.Tools.ShaderCompiler.Tests;

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
    public void CompilesToSpirv()
    {
        ShaderSource source = new(VertexSource, "main", ShaderStage.Vertex);

        ShaderCompileResult result = _compiler.Compile(source);

        result.Succeeded.Should().BeTrue(result.Messages);
        uint magic = BitConverter.ToUInt32(result.Bytecode, 0);
        magic.Should().Be(0x07230203u);
    }

    [Fact]
    public void CompilesEveryStage()
    {
        ShaderSource pixel = new(PixelSource, "main", ShaderStage.Pixel);
        ShaderSource compute = new(ComputeSource, "main", ShaderStage.Compute);

        ShaderCompileResult pixelResult = _compiler.Compile(pixel);
        ShaderCompileResult computeResult = _compiler.Compile(compute);

        pixelResult.Succeeded.Should().BeTrue(pixelResult.Messages);
        computeResult.Succeeded.Should().BeTrue(computeResult.Messages);
    }

    [Fact]
    public void ReportsErrorsWithTheSourceName()
    {
        ShaderSource source = new("float4 main() : SV_Position { return missing; }", "main", ShaderStage.Vertex) { Name = "Broken.hlsl" };

        ShaderCompileResult result = _compiler.Compile(source);

        result.Succeeded.Should().BeFalse();
        result.Bytecode.Should().BeEmpty();
        result.Messages.Should().Contain("Broken.hlsl").And.Contain("undeclared identifier 'missing'");
    }

    [Fact]
    public void FailsWhenTheEntryPointIsMissing()
    {
        ShaderSource source = new(VertexSource, "VSMain", ShaderStage.Vertex);

        ShaderCompileResult result = _compiler.Compile(source);

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

        ShaderCompileResult result = _compiler.Compile(defined);

        result.Succeeded.Should().BeTrue(result.Messages);
    }

    [Fact]
    public void ReadsIncludesNextToTheShaderAndInIncludeFolders()
    {
        using TemporaryFolder folder = new();
        folder.Write("Shaders/Colors.hlsli", "float4 Red() { return float4(1, 0, 0, 1); }");
        folder.Write("Common/Math.hlsli", "float Twice(float value) { return value * 2; }");
        const string source = """
            #include "Colors.hlsli"
            #include "Math.hlsli"

            float4 main() : SV_Position
            {
                return Red() * Twice(1);
            }
            """;
        string name = folder.FullPath("Shaders/Main.hlsl");
        string includeFolder = folder.FullPath("Common");
        ShaderSource shader = new(source, "main", ShaderStage.Vertex) { Name = name, IncludeDirectories = [includeFolder] };

        ShaderCompileResult result = _compiler.Compile(shader);

        result.Succeeded.Should().BeTrue(result.Messages);
    }

    [Fact]
    public void AMissingIncludeFails()
    {
        ShaderSource shader = new("#include \"Missing.hlsli\"\nfloat4 main() : SV_Position { return 0; }", "main", ShaderStage.Vertex);

        ShaderCompileResult result = _compiler.Compile(shader);

        result.Succeeded.Should().BeFalse();
        result.Messages.Should().Contain("Missing.hlsli");
    }
}
