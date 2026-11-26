// Lens flares after Froyok's: the half-size scene goes to quarter size through the bloom's 13-tap filter with a soft threshold
// (ThresholdMain), one Dual Kawase blur steadies it (BlurMain), and the ghosts copy it scaled about the middle, negative ones
// mirrored, their colours split along the line from the middle and faded at their rims and the screen's edge (GhostsMain).

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);
RWTexture2D<float4> RWTextures[] : register(u0, space4);

struct FlareConstants
{
    float2 SourceTexel;
    float2 DestinationSize;
    uint Source;
    uint Destination;
    uint Sampler;
    uint Ghosts;
    uint GhostCount;
    float Threshold;
    float ThresholdRange;
    float Intensity;
    float ChromaShift;
    float EdgeFade;
};

[[vk::push_constant]] ConstantBuffer<FlareConstants> Pass : register(b0, space0);

static const float3 ToLuminance = float3(0.2126, 0.7152, 0.0722);

// The byte size of one ghost in the ghost buffer: its colour, then its scale.
static const uint GhostStride = 16;

float3 Fetch(float2 uv)
{
    return Textures[Pass.Source].SampleLevel(Samplers[Pass.Sampler], uv, 0).rgb;
}

[numthreads(8, 8, 1)]
void ThresholdMain(uint3 id : SV_DispatchThreadID)
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

    // A soft threshold: nothing below the level, then rising over the range to the whole light.
    float luminance = dot(color, ToLuminance);
    float keep = saturate((luminance - Pass.Threshold) / max(Pass.ThresholdRange, 1e-4));
    RWTextures[Pass.Destination][id.xy] = float4(color * keep, 1);
}

[numthreads(8, 8, 1)]
void BlurMain(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= (uint2)Pass.DestinationSize))
    {
        return;
    }

    float2 uv = (id.xy + 0.5) / Pass.DestinationSize;
    float2 t = Pass.SourceTexel;
    float3 color = Fetch(uv) * 4;
    color += Fetch(uv + t * float2(-1, -1)) + Fetch(uv + t * float2(1, -1)) + Fetch(uv + t * float2(-1, 1)) + Fetch(uv + t * float2(1, 1));
    RWTextures[Pass.Destination][id.xy] = float4(color / 8, 1);
}

// How much of a ghost sampled at this uv shows: faded toward its own rim (far from the middle) and as it leaves the screen.
float GhostMask(float2 uv)
{
    float rim = 1 - smoothstep(0.6, 1, length(uv * 2 - 1));
    float2 edges = smoothstep(0, Pass.EdgeFade, uv) * smoothstep(0, Pass.EdgeFade, 1 - uv);
    return rim * edges.x * edges.y;
}

[numthreads(8, 8, 1)]
void GhostsMain(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= (uint2)Pass.DestinationSize))
    {
        return;
    }

    float2 uv = (id.xy + 0.5) / Pass.DestinationSize;
    float3 light = 0;
    for (uint index = 0; index < Pass.GhostCount; index++)
    {
        float4 ghost = asfloat(Buffers[Pass.Ghosts].Load4(index * GhostStride));
        float2 ghostUv = (uv - 0.5) * ghost.w + 0.5;
        float2 outward = normalize(ghostUv - 0.5 + 1e-5) * Pass.ChromaShift;
        float red = Fetch(ghostUv + outward).r;
        float green = Fetch(ghostUv).g;
        float blue = Fetch(ghostUv - outward).b;
        light += float3(red, green, blue) * ghost.rgb * GhostMask(ghostUv);
    }

    RWTextures[Pass.Destination][id.xy] = float4(light * Pass.Intensity, 1);
}
