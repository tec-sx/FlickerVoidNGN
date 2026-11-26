// What the engine's masters share: a painted layer laid over a surface and a tint varying by place.

#pragma once

// A number from 0 to 1 that changes from one cell of the size to the next, the same in every frame.
float CellNoise(float3 position, float size)
{
    float3 cell = floor(position / max(size, 0.01));
    return frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

// Brightens or darkens the color a little from patch to patch, by up to a quarter at full variation.
float3 VaryTint(float3 color, float3 position, float variation, float size)
{
    float shade = CellNoise(position, size) * 2 - 1;
    return color * (1 + shade * variation * 0.25);
}

// Lays a layer of the tint, times its painting where it has one (its alpha is where it lies), over the color by the amount.
float3 LayOver(float3 color, uint painting, float3 tint, float scale, float amount, SamplerState sampler, float2 uv)
{
    float3 layer = tint;
    if (painting != None)
    {
        float4 painted = Textures[NonUniformResourceIndex(painting)].Sample(sampler, uv * scale);
        layer *= painted.rgb;
        amount *= painted.a;
    }

    return lerp(color, layer, saturate(amount));
}
