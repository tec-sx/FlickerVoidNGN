// Bloom as in Call of Duty's 2014 talk: each level halves the one above with a 13-tap filter (DownMain), then from the
// smallest up each level adds a tent-filtered copy of the level below it (UpMain).

Texture2D Textures[] : register(t0, space1);
SamplerState Samplers[] : register(s0, space3);
RWTexture2D<float4> RWTextures[] : register(u0, space4);

struct BloomConstants
{
    float2 SourceTexel;
    float2 DestinationSize;
    uint Source;
    uint Destination;
    uint Sampler;
};

[[vk::push_constant]] ConstantBuffer<BloomConstants> Pass : register(b0, space0);

float3 Fetch(float2 uv)
{
    return Textures[Pass.Source].SampleLevel(Samplers[Pass.Sampler], uv, 0).rgb;
}

[numthreads(8, 8, 1)]
void DownMain(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= (uint2)Pass.DestinationSize))
    {
        return;
    }

    float2 uv = (id.xy + 0.5) / Pass.DestinationSize;
    float2 t = Pass.SourceTexel;
    float3 a = Fetch(uv + t * float2(-2, -2));
    float3 b = Fetch(uv + t * float2(0, -2));
    float3 c = Fetch(uv + t * float2(2, -2));
    float3 d = Fetch(uv + t * float2(-2, 0));
    float3 e = Fetch(uv);
    float3 f = Fetch(uv + t * float2(2, 0));
    float3 g = Fetch(uv + t * float2(-2, 2));
    float3 h = Fetch(uv + t * float2(0, 2));
    float3 i = Fetch(uv + t * float2(2, 2));
    float3 j = Fetch(uv + t * float2(-1, -1));
    float3 k = Fetch(uv + t * float2(1, -1));
    float3 l = Fetch(uv + t * float2(-1, 1));
    float3 m = Fetch(uv + t * float2(1, 1));
    float3 color = e * 0.125 + (a + c + g + i) * 0.03125 + (b + d + f + h) * 0.0625 + (j + k + l + m) * 0.125;
    RWTextures[Pass.Destination][id.xy] = float4(color, 1);
}

[numthreads(8, 8, 1)]
void UpMain(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= (uint2)Pass.DestinationSize))
    {
        return;
    }

    float2 uv = (id.xy + 0.5) / Pass.DestinationSize;
    float2 t = Pass.SourceTexel;
    float3 color = Fetch(uv) * 4;
    color += (Fetch(uv + t * float2(-1, 0)) + Fetch(uv + t * float2(1, 0)) + Fetch(uv + t * float2(0, -1)) + Fetch(uv + t * float2(0, 1))) * 2;
    color += Fetch(uv + t * float2(-1, -1)) + Fetch(uv + t * float2(1, -1)) + Fetch(uv + t * float2(-1, 1)) + Fetch(uv + t * float2(1, 1));
    float4 current = RWTextures[Pass.Destination][id.xy];
    RWTextures[Pass.Destination][id.xy] = float4(current.rgb + color / 16, 1);
}
