// The material buffer's parameters, as Scene.fvshader declares them and MaterialLayout packs them: the geometry pass reads a
// surface's texture, tint, relief and cutoff, the lighting pass and the probes' rays its shading controls, and shadows its
// cutoff.

#pragma once

#include "Frame.hlsl"

// The parameters Scene.fvshader declares, in its order.
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
