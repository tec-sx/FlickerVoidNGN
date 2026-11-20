namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>HLSL the tests draw with: bindless tables, push constants holding their indices, and vertices pulled from buffers.</summary>
internal static class TestShaders
{
    public const string Bindless = """
        Texture2D Textures[] : register(t0, space1);
        ByteAddressBuffer Buffers[] : register(t0, space2);
        SamplerState Samplers[] : register(s0, space3);
        RWTexture2D<float4> RWTextures[] : register(u0, space4);

        struct DrawConstants
        {
            uint4 Indices;
            float4 Color;
        };

        [[vk::push_constant]] ConstantBuffer<DrawConstants> Draw : register(b0, space0);

        struct VertexOutput
        {
            float4 Position : SV_Position;
            float2 Uv : TEXCOORD0;
        };
        """;

    /// <summary>Reads float3 positions (x, y in clip space, z depth) from Buffers[Indices.x].</summary>
    public const string PulledVertex = Bindless + """

        VertexOutput main(uint vertexId : SV_VertexID)
        {
            float3 position = Buffers[Draw.Indices.x].Load<float3>(vertexId * 12);
            VertexOutput output;
            output.Position = float4(position, 1);
            output.Uv = position.xy * float2(0.5, -0.5) + 0.5;
            return output;
        }
        """;

    /// <summary>A triangle covering the whole target, with uv 0,0 at the top left.</summary>
    public const string FullScreenVertex = Bindless + """

        VertexOutput main(uint vertexId : SV_VertexID)
        {
            float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
            VertexOutput output;
            output.Position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
            output.Uv = uv;
            return output;
        }
        """;

    public const string SolidColorPixel = Bindless + """

        float4 main(VertexOutput input) : SV_Target
        {
            return Draw.Color;
        }
        """;

    /// <summary>Samples Textures[Indices.y] with Samplers[Indices.z].</summary>
    public const string TexturedPixel = Bindless + """

        float4 main(VertexOutput input) : SV_Target
        {
            return Textures[Draw.Indices.y].Sample(Samplers[Draw.Indices.z], input.Uv);
        }
        """;

    /// <summary>Writes each 4-byte word's index times 3 into the writable buffer at Indices.x, one thread per word.</summary>
    public const string WriteBufferCompute = """
        RWByteAddressBuffer RWBuffers[] : register(u0, space2);

        struct DrawConstants
        {
            uint4 Indices;
            float4 Color;
        };

        [[vk::push_constant]] ConstantBuffer<DrawConstants> Draw : register(b0, space0);

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            RWBuffers[Draw.Indices.x].Store(id.x * 4, id.x * 3);
        }
        """;

    /// <summary>Fills the writable texture at Indices.y with Color, one thread per texel.</summary>
    public const string FillTextureCompute = Bindless + """

        [numthreads(8, 8, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            RWTextures[Draw.Indices.y][id.xy] = Draw.Color;
        }
        """;
}
