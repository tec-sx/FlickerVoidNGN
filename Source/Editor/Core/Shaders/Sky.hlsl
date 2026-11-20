// The editor's sky: one triangle at the far plane fills only what the scene left empty, with a gradient like an atmosphere,
// dark ground below the horizon and a lighter warm haze above it fading to a muted blue overhead. A perspective view shades by
// where each pixel's view ray points; an orthographic one, whose rays all point the same way, by height on the near plane.

struct SkyConstants
{
    // The view's matrices multiply row vectors, as System.Numerics' do.
    row_major float4x4 InverseViewProjection;
    float Orthographic;
    float3 Padding;
};

[[vk::push_constant]] ConstantBuffer<SkyConstants> Sky : register(b0, space0);

// sRGB colours, turned linear before writing to the sRGB target.
static const float3 Zenith = float3(0.23, 0.33, 0.40);
static const float3 Horizon = float3(0.54, 0.52, 0.44);
static const float3 GroundNear = float3(0.17, 0.16, 0.13);
static const float3 GroundFar = float3(0.07, 0.065, 0.05);

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 Clip : TEXCOORD0;
};

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    VertexOutput output;
    output.Clip = corner * 2 - 1;
    output.Position = float4(output.Clip, 1, 1);
    return output;
}

float3 WorldAt(float2 clip, float depth)
{
    float4 world = mul(float4(clip, depth, 1), Sky.InverseViewProjection);
    return world.xyz / world.w;
}

// From -1 straight down to 1 straight up, 0 at the horizon.
float Elevation(float2 clip)
{
    float3 nearPoint = WorldAt(clip, 0);
    if (Sky.Orthographic > 0.5)
    {
        float3 top = WorldAt(float2(0, -1), 0);
        float3 bottom = WorldAt(float2(0, 1), 0);
        float3 centre = WorldAt(float2(0, 0), 0);
        return (nearPoint.y - centre.y) / max(length(top - bottom), 0.0001);
    }

    float3 farPoint = WorldAt(clip, 1);
    float3 direction = normalize(farPoint - nearPoint);
    return direction.y;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    float elevation = Elevation(input.Clip);
    float3 color;
    if (elevation >= 0)
    {
        color = lerp(Horizon, Zenith, smoothstep(0, 0.6, elevation));
    }
    else
    {
        color = lerp(GroundNear, GroundFar, smoothstep(0, 0.4, -elevation));
    }

    return float4(pow(color, 2.2), 1);
}
