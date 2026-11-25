// From the geometry pass's depth and normals, works out each pixel's ambient occlusion (red: the share of ambient light nearby
// surfaces let through) and contact shadow (green: 0 where something close blocks the sun, which the shadow map is too coarse to
// see).

#include "Frame.hlsl"
#include "GBuffer.hlsl"

struct ScreenShadowConstants
{
    uint Frame;
    uint Output;
};

[[vk::push_constant]] ConstantBuffer<ScreenShadowConstants> Pass : register(b0, space0);

static const uint OcclusionSamples = 8;
static const uint ContactSteps = 8;

// Points in the unit hemisphere round +Z, nearer the centre more often, as SSAO kernels do.
static const float3 Kernel[OcclusionSamples] =
{
    float3(0.38, 0.12, 0.31), float3(-0.29, 0.36, 0.42), float3(-0.18, -0.47, 0.26), float3(0.51, -0.33, 0.55),
    float3(0.08, 0.71, 0.37), float3(-0.66, -0.08, 0.51), float3(0.24, -0.15, 0.88), float3(-0.41, 0.58, 0.63),
};

float LoadDepth(FrameData frame, int2 pixel)
{
    return Textures[frame.Depth].Load(int3(pixel, 0)).r;
}

float PixelNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

// How far behind the depth buffer a point is, in world units in front of the camera, or a negative value in front of it.
// Points off screen count as in front.
float DepthBehindScene(FrameData frame, float3 position)
{
    float3 projected = ProjectToPixel(frame, position);
    if (any(projected.xy < 0) || any(projected.xy >= frame.TargetSize))
    {
        return -1;
    }

    float sceneDepthValue = LoadDepth(frame, int2(projected.xy));
    float3 scene = WorldAtPixel(frame, floor(projected.xy) + 0.5, sceneDepthValue);
    float sceneDepth = dot(float4(scene, 1), frame.DepthAxis);
    return projected.z - sceneDepth;
}

float AmbientOcclusion(FrameData frame, float3 position, float3 normal, float noise)
{
    if (frame.AmbientOcclusion <= 0)
    {
        return 1;
    }

    // A basis round the normal, turned by the noise so neighbouring pixels sample differently.
    float angle = noise * 6.2831853;
    float3 helper = abs(normal.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 tangent = normalize(cross(helper, normal));
    float3 bitangent = cross(normal, tangent);
    float3 turnedTangent = tangent * cos(angle) + bitangent * sin(angle);
    float3 turnedBitangent = cross(normal, turnedTangent);

    float radius = frame.AmbientOcclusionRadius;
    float occlusion = 0;
    for (uint index = 0; index < OcclusionSamples; index++)
    {
        float3 offset = Kernel[index];
        float3 sample = position + (turnedTangent * offset.x + turnedBitangent * offset.y + normal * offset.z) * radius;
        float behind = DepthBehindScene(frame, sample);
        // Count samples hidden behind nearby surfaces, fading out ones hidden by surfaces much nearer the camera.
        if (behind > 0.02 * radius)
        {
            occlusion += saturate(radius / behind);
        }
    }

    return 1 - frame.AmbientOcclusion * occlusion / OcclusionSamples;
}

float ContactShadow(FrameData frame, float3 position, float3 normal, float noise)
{
    float reach = frame.ContactShadowLength;
    if (reach <= 0)
    {
        return 1;
    }

    float3 towardSun = -normalize(frame.SunDirection);
    if (dot(normal, towardSun) <= 0)
    {
        return 1;
    }

    float3 start = position + normal * (reach * 0.05);
    for (uint index = 0; index < ContactSteps; index++)
    {
        float travelled = (index + noise) / ContactSteps * reach;
        float3 sample = start + towardSun * travelled;
        float behind = DepthBehindScene(frame, sample);
        // Hidden behind a surface no thicker than the march: something stands between the point and the sun.
        if (behind > 0.01 && behind < reach)
        {
            return 0;
        }
    }

    return 1;
}

[numthreads(8, 8, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    if (any(id.xy >= (uint2)frame.TargetSize))
    {
        return;
    }

    int2 pixel = int2(id.xy);
    float depth = LoadDepth(frame, pixel);
    if (depth >= 1)
    {
        RWTextures[Pass.Output][id.xy] = float4(1, 1, 0, 1);
        return;
    }

    float3 position = WorldAtPixel(frame, pixel + 0.5, depth);
    float2 folded = Textures[frame.GBufferNormal].Load(int3(pixel, 0)).xy;
    float3 normal = DecodeNormal(folded);

    float noise = PixelNoise(pixel);
    float occlusion = AmbientOcclusion(frame, position, normal, noise);
    float contact = ContactShadow(frame, position, normal, noise);
    RWTextures[Pass.Output][id.xy] = float4(occlusion, contact, 0, 1);
}
