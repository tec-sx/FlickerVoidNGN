// The material buffer's parameters, as Scene.fvshader declares them and MaterialLayout packs them, and the master's shading model: the geometry pass reads a
// surface's texture, tint, relief and cutoff, the lighting pass and the probes' rays its shading controls, and shadows its
// cutoff.

#pragma once

#include "Frame.hlsl"

// MaterialLayout's shading models (ShadingModel.cs), in its order.
static const uint ShadingPainted = 0;
static const uint ShadingMetal = 1;
static const uint ShadingGlow = 2;

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

// The share of light a surface takes at the cosine between its normal and the light: wrapped round past the terminator,
// then pulled toward a hard step as the ramp softness falls (1 is plain wrapped diffuse light).
float Diffuse(Material material, float cosine)
{
    float wrapped = saturate((cosine + material.Wrap) / (1 + material.Wrap));
    float halfWidth = max(0.5 * material.RampSoftness, 0.001);
    float stepped = smoothstep(0.5 - halfWidth, 0.5 + halfWidth, wrapped);
    return lerp(stepped, wrapped, material.RampSoftness);
}

// The light leaving a surface of the color lit by the diffuse and specular light, by its master's shading model: paint, metal
// under paint (less diffuse light, the highlight and a reflection of the light about it in its own color), or paint that glows.
float3 ShadeSurface(Material material, float3 albedo, float3 diffuse, float3 specular, float3 ambient)
{
    if (material.ShadingModel == ShadingMetal)
    {
        float metalness = saturate(material.ModelValues.x);
        float3 highlight = lerp(1, albedo, metalness);
        float3 reflection = albedo * ambient * metalness * saturate(material.Specular);
        return albedo * diffuse * (1 - metalness) + specular * highlight + reflection;
    }

    if (material.ShadingModel == ShadingGlow)
    {
        float glow = material.ModelValues.x;
        float lit = material.ModelValues.y;
        return albedo * (diffuse * lit + glow) + specular;
    }

    return albedo * diffuse + specular;
}
