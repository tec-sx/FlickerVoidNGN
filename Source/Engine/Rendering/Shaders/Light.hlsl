// A point or spot light as the frame buffer holds it after the frame data (GpuLight.cs), and how much of it reaches a point,
// shared by the lighting pass and the rays traced toward it.

#pragma once

#include "Frame.hlsl"

// RayShadow is the light's channel of the traced shadows, or None; the sun's is ShadowRaySun.
struct Light
{
    float3 Position;
    float Range;
    float3 Color;
    float CosOuter;
    float3 Direction;
    float CosInner;
    float3 Right;
    uint Cookie;
    uint Shadow;
    float SourceRadius;
    uint RayShadow;
};

static const uint LightSize = 80;

static const uint ShadowRaySun = 0;

// How short of a lamp a ray stops, so the lamp's own bulb or shade standing round it doesn't hide it.
static const float LampClearance = 0.01;

Light LoadLight(FrameData frame, uint index)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    return buffer.Load<Light>(FrameDataSize + index * LightSize);
}

// The light arriving at a point before its cookie and shadow: inverse-square falloff softened near the light and windowed to
// reach zero at its range, in the spot cone; direction points toward the light and distance is how far it is.
float3 LightReach(Light light, float3 worldPosition, out float3 direction, out float distance)
{
    float3 toLight = light.Position - worldPosition;
    float distanceSquared = dot(toLight, toLight);
    distance = sqrt(distanceSquared);
    direction = toLight / max(distance, 1e-4);
    float ratio = distanceSquared / (light.Range * light.Range);
    float window = saturate(1 - ratio * ratio);
    float falloff = window * window / (distanceSquared + 1);
    float cone = smoothstep(light.CosOuter, light.CosInner, dot(-direction, light.Direction));
    return light.Color * (falloff * cone);
}

// The cookie's color where the light's ray through the point crosses it, the outer cone filling the image.
float3 SampleCookie(FrameData frame, Light light, float3 fromLight)
{
    float3 up = cross(light.Right, light.Direction);
    float3 local = float3(dot(fromLight, light.Right), dot(fromLight, up), dot(fromLight, light.Direction));
    float tanOuter = sqrt(saturate(1 - light.CosOuter * light.CosOuter)) / light.CosOuter;
    float2 spread = local.xy / (local.z * tanOuter);
    float2 uv = float2(0.5 + 0.5 * spread.x, 0.5 - 0.5 * spread.y);
    // Level 0: the light loop isn't uniform across pixels, so there are no derivatives to pick a mip with; for the same reason
    // neighbouring pixels may read different cookies.
    Texture2D cookie = Textures[NonUniformResourceIndex(light.Cookie)];
    return cookie.SampleLevel(Samplers[frame.ClampSampler], uv, 0).rgb;
}
