// Writes what a denoising upscaler reads of each pixel's surface beside the frame (DLSS Ray Reconstruction's guides), at the
// render size, a thread a pixel: the albedo the lighting pass multiplies the diffuse light by, linear and muted and greyed as
// the lit surface is; the share of specular light it reflects toward the view, the fit NVIDIA's Ray Reconstruction guide gives
// (Ray Tracing Gems, chapter 32) for the material's highlight strength at the roughness its glossiness matches; and its world
// normal with that roughness in alpha. Where nothing was drawn: middle grey, black and zeros, as the guide asks.

#include "Frame.hlsl"
#include "GBuffer.hlsl"
#include "Material.hlsl"

struct DenoiseGuideConstants
{
    uint Frame;
    uint DiffuseAlbedo;
    uint SpecularAlbedo;
    uint NormalRoughness;
};

[[vk::push_constant]] ConstantBuffer<DenoiseGuideConstants> Pass : register(b0, space0);

// The microfacet alpha of the highlight Lighting.hlsl's Blinn-Phong exponent draws for the glossiness (Walter et al. 2007,
// "Microfacet Models for Refraction through Rough Surfaces"); the roughness is its square root.
float HighlightAlpha(Material material)
{
    float exponent = exp2(1 + material.Glossiness * 10);
    return sqrt(2 / (exponent + 2));
}

// The average share of light a surface of the reflectance head on reflects toward the view, at the alpha and the cosine
// between its normal and the view (EnvBRDFApprox2, Ray Tracing Gems, chapter 32).
float3 EnvBRDFApprox2(float3 specularColor, float alpha, float facing)
{
    facing = abs(facing);
    float4 x = float4(1, facing, facing * facing, facing * facing * facing);
    float4 y = float4(1, alpha, alpha * alpha, alpha * alpha * alpha);
    float2x2 m1 = float2x2(0.99044, -1.28514, 1.29678, -0.755907);
    float3x3 m2 = float3x3(1, 2.92338, 59.4188, 20.3225, -27.0302, 222.592, 121.563, 626.13, 316.627);
    float2x2 m3 = float2x2(0.0365463, 3.32707, 9.0632, -9.04756);
    float3x3 m4 = float3x3(1, 3.59685, -1.36772, 9.04401, -16.3174, 9.22949, 5.56589, 19.7886, -20.2123);
    float bias = dot(mul(m1, x.xy), y.xy) * rcp(dot(mul(m2, x.xyw), y.xyw));
    float scale = dot(mul(m3, x.xy), y.xy) * rcp(dot(mul(m4, x.xzw), y.xyw));
    // No reflectance reflects nothing, however grazing the view.
    bias *= saturate(specularColor.g * 50);
    return mad(specularColor, max(0, scale), max(0, bias));
}

[numthreads(8, 8, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    RWTexture2D<float4> diffuseAlbedo = RWTextures[Pass.DiffuseAlbedo];
    RWTexture2D<float4> specularAlbedo = RWTextures[Pass.SpecularAlbedo];
    RWTexture2D<float4> normalRoughness = RWTextures[Pass.NormalRoughness];
    int3 texel = int3(id.xy, 0);
    float depth = Textures[frame.Depth].Load(texel).r;
    if (depth >= 1)
    {
        diffuseAlbedo[id.xy] = float4(0.5, 0.5, 0.5, 0);
        specularAlbedo[id.xy] = 0;
        normalRoughness[id.xy] = 0;
        return;
    }

    float2 pixel = float2(id.xy) + 0.5;
    float3 worldPosition = WorldAtPixel(frame, pixel, depth);
    float3 albedo = Textures[frame.GBufferAlbedo].Load(texel).rgb;
    float2 folded = Textures[frame.GBufferNormal].Load(texel).xy;
    float3 normal = DecodeNormal(folded);
    float4 materialBytes = Textures[frame.GBufferMaterial].Load(texel);
    SurfaceMaterial surface = DecodeMaterial(materialBytes);
    Material material = LoadMaterial(surface.Material);
    float alpha = HighlightAlpha(material);
    float3 toCamera = ToCamera(frame, worldPosition);
    float3 specular = EnvBRDFApprox2(saturate(material.Specular), alpha, dot(normal, toCamera));
    albedo = MuteOutside(frame, albedo, worldPosition);
    specular = MuteOutside(frame, specular, worldPosition);
    if ((surface.Flags & FlagGreyed) != 0)
    {
        albedo = dot(albedo, float3(0.2126, 0.7152, 0.0722));
    }

    diffuseAlbedo[id.xy] = float4(albedo, 0);
    specularAlbedo[id.xy] = float4(specular, 0);
    normalRoughness[id.xy] = float4(normal, sqrt(alpha));
}
