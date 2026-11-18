namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>HLSL the tests draw with: bindless tables, push constants holding their indices, and vertices pulled from buffers.</summary>
internal static class TestShaders
{
    public const string Bindless = """
        Texture2D Textures[] : register(t0, space1);
        ByteAddressBuffer Buffers[] : register(t0, space2);
        SamplerState Samplers[] : register(s0, space3);

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
}
