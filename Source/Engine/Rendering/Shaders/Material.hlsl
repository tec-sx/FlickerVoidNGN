// The material buffer's parameters, as Scene.fvshader declares them and MaterialLayout packs them, and the master's shading model: the geometry pass reads a
// surface's texture, tint, relief and cutoff, the lighting pass and the probes' rays its shading controls, and shadows its
// cutoff.

#pragma once

#include "Frame.hlsl"

// MaterialLayout's shading models (ShadingModel.cs), in its order.
static const uint ShadingPainted = 0;
static const uint ShadingMetal = 1;
static const uint ShadingGlow = 2;
static const uint ShadingSkin = 3;

// The parameters Scene.fvshader declares, in its order, then the master's shading model and the values it reads.
struct Material
{
    float3 Tint;
    float RampSoftness;
    float Wrap;
    float3 ShadowTint;
    float3 RimColor;
    float RimStrength;
    float RimPower;
    float Specular;
    float Glossiness;
    float3 Emissive;
    float EmissiveIntensity;
    uint Texture;
    uint Relief;
    float TextureScale;
    float ReliefDepth;
    float Cutoff;
    uint ShadingModel;
    float4 ModelValues;
};

// The material's parameters, packed tightly in declared order by MaterialLayout; neighbouring pixels and rays may read different
// materials, so the index is marked as varying.
Material LoadMaterial(uint index)
{
    ByteAddressBuffer buffer = Buffers[NonUniformResourceIndex(index)];
    Material material;
    material.Tint = buffer.Load<float3>(0);
    material.RampSoftness = buffer.Load<float>(12);
    material.Wrap = buffer.Load<float>(16);
    material.ShadowTint = buffer.Load<float3>(20);
    material.RimColor = buffer.Load<float3>(32);
    material.RimStrength = buffer.Load<float>(44);
    material.RimPower = buffer.Load<float>(48);
    material.Specular = buffer.Load<float>(52);
    material.Glossiness = buffer.Load<float>(56);
    material.Emissive = buffer.Load<float3>(60);
    material.EmissiveIntensity = buffer.Load<float>(72);
    material.Texture = buffer.Load<uint>(76);
    material.Relief = buffer.Load<uint>(80);
    material.TextureScale = buffer.Load<float>(84);
    material.ReliefDepth = buffer.Load<float>(88);
    material.Cutoff = buffer.Load<float>(92);
    material.ShadingModel = buffer.Load<uint>(96);
    material.ModelValues = buffer.Load<float4>(100);
    return material;
}

// The share of light a surface takes at the cosine between its normal and the light, wrapped round past the terminator by the
// wrap, then pulled toward a hard step as the ramp softness falls (1 is plain wrapped diffuse light).
float WrappedDiffuse(Material material, float wrap, float cosine)
{
    float wrapped = saturate((cosine + wrap) / (1 + wrap));
    float halfWidth = max(0.5 * material.RampSoftness, 0.001);
    float stepped = smoothstep(0.5 - halfWidth, 0.5 + halfWidth, wrapped);
    return lerp(stepped, wrapped, material.RampSoftness);
}

// The share of each color of light a surface takes at the cosine between its normal and the light. Skin wraps each color further
// by how far it scatters under the surface (its scatter color times its width), so light bleeds red round the terminator, a cheap
// subsurface scattering (Green 2004, "Real-Time Approximations to Subsurface Scattering", GPU Gems, chapter 16).
float3 Diffuse(Material material, float cosine)
{
    if (material.ShadingModel == ShadingSkin)
    {
        float3 wraps = material.Wrap + saturate(material.ModelValues.rgb) * material.ModelValues.a;
        float red = WrappedDiffuse(material, wraps.r, cosine);
        float green = WrappedDiffuse(material, wraps.g, cosine);
        float blue = WrappedDiffuse(material, wraps.b, cosine);
        return float3(red, green, blue);
    }

    float diffuse = WrappedDiffuse(material, material.Wrap, cosine);
    return diffuse;
}

// How much of the world a glossy surface that is not metal reflects toward the view, more toward its edges (Schlick's Fresnel
// factor from a dielectric's head-on reflectance), scaled by its highlight's strength and glossiness.
float Sheen(Material material, float facing)
{
    float fresnel = 0.04 + 0.96 * pow(1 - saturate(facing), 5);
    return fresnel * saturate(material.Specular) * material.Glossiness;
}

// The light leaving a surface of the color lit by the diffuse and specular light, by its master's shading model: paint, metal
// under paint (less diffuse light, the highlight and its reflection of the world in its own color), or paint that glows. The
// reflection is the world in the mirror direction (or the ambient light where the frame reflects no world), and the sheen how
// much of it paint reflects.
float3 ShadeSurface(Material material, float3 albedo, float3 diffuse, float3 specular, float3 reflection, float sheen)
{
    if (material.ShadingModel == ShadingMetal)
    {
        float metalness = saturate(material.ModelValues.x);
        float3 highlight = lerp(1, albedo, metalness);
        float3 mirrored = albedo * reflection * metalness * saturate(material.Specular);
        return albedo * diffuse * (1 - metalness) + specular * highlight + mirrored + reflection * sheen * (1 - metalness);
    }

    if (material.ShadingModel == ShadingGlow)
    {
        float glow = material.ModelValues.x;
        float lit = material.ModelValues.y;
        return albedo * (diffuse * lit + glow) + specular + reflection * sheen;
    }

    return albedo * diffuse + specular + reflection * sheen;
}
