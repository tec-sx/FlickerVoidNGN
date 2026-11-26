// The light functions the lighting pass (Lighting.hlsl) and the transparent pass (Transparent.hlsl) share: the highlight, the
// shadow atlas and traced shadows, the clusters' lamps, the fog and the screen shadows' blur.

#pragma once

#include "Frame.hlsl"
#include "GBuffer.hlsl"
#include "Light.hlsl"
#include "Material.hlsl"
#include "Probes.hlsl"

struct Shadow
{
    float4x4 WorldToAtlas;
    float4 Rect;
    float TexelPerDistance;
    float TexelConstant;
};

static const uint ShadowSize = 96;

static const float AtlasSize = 4096;

// Normal offset in texels and depth bias that keep surfaces from shadowing themselves.
static const float NormalBias = 1.5;

static const float DepthBias = 0.0003;

// The cluster grid of LightClusters.
static const uint TilesX = 16;

static const uint TilesY = 9;

static const uint Slices = 24;

// A Blinn-Phong highlight, sharper as the glossiness rises, on surfaces facing the light.
float Specular(Material material, float3 normal, float3 toLight, float3 toCamera)
{
    if (material.Specular <= 0 || dot(normal, toLight) <= 0)
    {
        return 0;
    }

    float3 halfway = normalize(toLight + toCamera);
    float exponent = exp2(1 + material.Glossiness * 10);
    return material.Specular * pow(saturate(dot(normal, halfway)), exponent);
}

// The point light shadow face whose view holds the offset from the light, by its largest axis: +X, -X, +Y, -Y, +Z, -Z.
uint PointFace(float3 offset)
{
    float3 size = abs(offset);
    if (size.x >= size.y && size.x >= size.z)
    {
        return offset.x >= 0 ? 0 : 1;
    }

    if (size.y >= size.z)
    {
        return offset.y >= 0 ? 2 : 3;
    }

    return offset.z >= 0 ? 4 : 5;
}

uint FindCluster(FrameData frame, float2 pixel, float3 worldPosition)
{
    uint2 tile = min(uint2(pixel * float2(TilesX, TilesY) / frame.TargetSize), uint2(TilesX - 1, TilesY - 1));
    float depth = dot(float4(worldPosition, 1), frame.DepthAxis);
    if (frame.LogarithmicSlices != 0)
    {
        depth = log(max(depth, 1e-6));
    }

    float slice = clamp(floor(depth * frame.SliceScale + frame.SliceBias), 0, Slices - 1);
    return ((uint)slice * TilesY + tile.y) * TilesX + tile.x;
}

Shadow LoadShadow(FrameData frame, uint index)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    uint offset = frame.ShadowsOffset + index * ShadowSize;
    Shadow shadow;
    shadow.WorldToAtlas = float4x4(buffer.Load<float4>(offset), buffer.Load<float4>(offset + 16), buffer.Load<float4>(offset + 32), buffer.Load<float4>(offset + 48));
    shadow.Rect = buffer.Load<float4>(offset + 64);
    shadow.TexelPerDistance = buffer.Load<float>(offset + 80);
    shadow.TexelConstant = buffer.Load<float>(offset + 84);
    return shadow;
}

// The share of four atlas texels around the point that the depth is in front of, blended by the point's place between them.
float ShadowTap(FrameData frame, float2 uv, float depth)
{
    float2 texel = uv * AtlasSize - 0.5;
    float2 blend = frac(texel);
    float2 gatherUv = (floor(texel) + 1) / AtlasSize;
    float4 depths = Textures[frame.ShadowAtlas].GatherRed(Samplers[frame.ClampSampler], gatherUv);
    // Gather returns texels (0, 1), (1, 1), (1, 0) and (0, 0), counting right and down.
    float4 lit = step(depth, depths);
    float top = lerp(lit.w, lit.z, blend.x);
    float bottom = lerp(lit.x, lit.y, blend.x);
    return lerp(top, bottom, blend.y);
}

// 1 where the shadow's view sees the point, 0 where something hides it, softened over a 3x3 texel filter, or over the
// four texels round it when not soft (fog takes many samples, each blurred by the march anyway).
float SampleShadow(FrameData frame, uint index, float3 worldPosition, float3 normal, float distance, bool soft)
{
    Shadow shadow = LoadShadow(frame, index);
    float texelSize = shadow.TexelPerDistance * distance + shadow.TexelConstant;
    float3 offsetPosition = worldPosition + normal * texelSize * NormalBias;
    float4 clip = mul(float4(offsetPosition, 1), shadow.WorldToAtlas);
    float3 atlas = clip.xyz / clip.w;
    bool insideTile = all(atlas.xy >= shadow.Rect.xy) && all(atlas.xy <= shadow.Rect.zw);
    if (!insideTile || atlas.z > 1)
    {
        return 1;
    }

    float depth = atlas.z - DepthBias;
    if (!soft)
    {
        return ShadowTap(frame, atlas.xy, depth);
    }

    float lit = 0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 uv = clamp(atlas.xy + float2(x, y) / AtlasSize, shadow.Rect.xy, shadow.Rect.zw);
            lit += ShadowTap(frame, uv, depth);
        }
    }

    return lit / 9;
}

// The share of the light's rays that reached the pixel's surface, its channel of the traced shadows.
float RayShadow(FrameData frame, int3 texel, uint channel)
{
    float4 visibility = Textures[frame.RayShadows].Load(texel);
    return visibility[channel];
}

// The light arriving at a point from the light: its reach (Light.hlsl), the cookie and the shadow; direction points toward the
// light. A surface's shadow is traced while the frame traces the light's, else its normal offsets the shadow map lookup over a
// soft filter; a point in fog has neither texel nor normal, and reads the shadow maps alone.
float3 IncomingLight(FrameData frame, Light light, float3 worldPosition, float3 normal, int3 texel, bool surface, out float3 direction)
{
    float distance;
    float3 color = LightReach(light, worldPosition, direction, distance);
    if (all(color <= 0))
    {
        return 0;
    }

    if (light.Cookie != None)
    {
        color *= SampleCookie(frame, light, -direction);
    }

    if (surface && light.RayShadow != None && frame.RayShadows != None)
    {
        color *= RayShadow(frame, texel, light.RayShadow);
    }
    else if (light.Shadow != None)
    {
        uint shadow = light.Shadow;
        if (light.CosOuter < -1)
        {
            shadow += PointFace(-direction);
        }

        color *= SampleShadow(frame, shadow, worldPosition, normal, distance, surface);
    }

    return color;
}

// Adds the diffuse and specular light of the pixel's cluster lights.
void ShadeClusterLights(FrameData frame, Material material, float2 pixel, float3 worldPosition, float3 normal, float3 toCamera, inout float3 diffuse, inout float3 specular)
{
    int3 texel = int3(pixel, 0);
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    uint cluster = FindCluster(frame, pixel, worldPosition);
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light clusterLight = LoadLight(frame, lightIndex);
        float3 direction;
        float3 incoming = IncomingLight(frame, clusterLight, worldPosition, normal, texel, true, direction);
        float cosine = dot(normal, direction);
        diffuse += incoming * Diffuse(material, cosine);
        specular += incoming * Specular(material, normal, direction, toCamera);
    }
}

// Henyey-Greenstein phase, scaled so even scattering is 1, for light turning by the angle whose cosine is given.
float Phase(float cosAngle, float anisotropy)
{
    float g = anisotropy;
    float denominator = 1 + g * g - 2 * g * cosAngle;
    return (1 - g * g) / (denominator * sqrt(denominator));
}

// The light a point in fog scatters toward the camera, from every light that reaches it, before the fog's color.
float3 FogLight(FrameData frame, float2 pixel, float3 position, float3 towardCamera)
{
    float3 light = frame.Ambient * IndoorAmbient(frame, position) + SampleProbes(frame, position, 0, towardCamera);
    float3 sunDirection = normalize(frame.SunDirection);
    float3 sun = (1 - frame.Ambient) * frame.SunLight * Phase(dot(sunDirection, towardCamera), frame.FogAnisotropy);
    if (frame.SunShadow != None)
    {
        sun *= SampleShadow(frame, frame.SunShadow, position, 0, 0, false);
    }

    light += sun;
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    uint cluster = FindCluster(frame, pixel, position);
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light clusterLight = LoadLight(frame, lightIndex);
        float3 direction;
        float3 incoming = IncomingLight(frame, clusterLight, position, 0, 0, false, direction);
        light += incoming * Phase(dot(-direction, towardCamera), frame.FogAnisotropy);
    }

    return light;
}

// Fog density at a point: full at and below the fog height, thinning exponentially above it.
float FogDensityAt(FrameData frame, float3 position)
{
    float above = max(position.y - frame.FogHeight, 0);
    return frame.FogDensity * exp(-above * frame.FogHeightFalloff);
}

// The fog volume: a grid of froxels, FogGrid.x by FogGrid.y across the render target and FogGrid.z slices deep along the view, from
// the near plane to the fog's range, each holding a float4 (FogVolumePass.cs).
static const uint3 FogGrid = uint3(160, 90, 64);

// The view depth at the boundary of a slice (0 is the near plane, FogGrid.z the range): logarithmic in a perspective view, even in
// an orthographic one.
float FogSliceDepth(FrameData frame, float slice)
{
    float near = max(frame.NearPlane, 1e-3);
    float range = max(frame.FogRange, near * 1.01);
    float along = slice / FogGrid.z;
    if (frame.LogarithmicSlices != 0)
    {
        return near * pow(range / near, along);
    }

    return lerp(near, range, along);
}

// The slice boundary a view depth lies at, the inverse of FogSliceDepth, clamped to the volume.
float FogSliceAt(FrameData frame, float depth)
{
    float near = max(frame.NearPlane, 1e-3);
    float range = max(frame.FogRange, near * 1.01);
    float along;
    if (frame.LogarithmicSlices != 0)
    {
        along = log(max(depth, near) / near) / log(range / near);
    }
    else
    {
        along = (depth - near) / (range - near);
    }

    return saturate(along) * FogGrid.z;
}

// The world position at a view depth along a pixel's ray, and the direction of the ray.
float3 FogPosition(FrameData frame, float2 pixel, float depth, out float3 direction)
{
    float3 nearPoint = WorldAtPixel(frame, pixel, 0);
    float3 farPoint = WorldAtPixel(frame, pixel, 1);
    direction = normalize(farPoint - nearPoint);
    float nearDepth = dot(float4(nearPoint, 1), frame.DepthAxis);
    float along = (depth - nearDepth) / dot(direction, frame.DepthAxis.xyz);
    return nearPoint + direction * along;
}

float4 LoadFogCell(ByteAddressBuffer volume, int3 cell)
{
    int3 clamped = clamp(cell, 0, int3(FogGrid) - 1);
    uint index = (clamped.z * FogGrid.y + clamped.y) * FogGrid.x + clamped.x;
    return volume.Load<float4>(index * 16);
}

// The volume at a place given in froxel coordinates, a froxel's centre at its index, trilinear.
float4 SampleFogCells(ByteAddressBuffer volume, float3 place)
{
    float3 base = floor(place);
    float3 fraction = place - base;
    int3 cell = int3(base);
    float4 near0 = lerp(LoadFogCell(volume, cell), LoadFogCell(volume, cell + int3(1, 0, 0)), fraction.x);
    float4 near1 = lerp(LoadFogCell(volume, cell + int3(0, 1, 0)), LoadFogCell(volume, cell + int3(1, 1, 0)), fraction.x);
    float4 far0 = lerp(LoadFogCell(volume, cell + int3(0, 0, 1)), LoadFogCell(volume, cell + int3(1, 0, 1)), fraction.x);
    float4 far1 = lerp(LoadFogCell(volume, cell + int3(0, 1, 1)), LoadFogCell(volume, cell + int3(1, 1, 1)), fraction.x);
    return lerp(lerp(near0, near1, fraction.y), lerp(far0, far1, fraction.y), fraction.z);
}

// The integrated volume at a boundary between slices (0 the near plane), where the pixel falls across the grid.
float4 FogBoundary(ByteAddressBuffer volume, float2 across, uint boundary)
{
    if (boundary == 0)
    {
        return float4(0, 0, 0, 1);
    }

    return SampleFogCells(volume, float3(across, min(boundary, FogGrid.z) - 1));
}

// The fog in front of a surface: the light it scatters toward the camera and the share of the surface's light it lets through.
float4 SampleFog(FrameData frame, float2 pixel, float depth)
{
    ByteAddressBuffer volume = Buffers[frame.FogVolume];
    float slice = FogSliceAt(frame, depth);
    float2 across = pixel / frame.TargetSize * float2(FogGrid.xy) - 0.5;
    uint lower = (uint)floor(slice);
    float4 before = FogBoundary(volume, across, lower);
    float4 after = FogBoundary(volume, across, lower + 1);
    return lerp(before, after, slice - lower);
}

// The pixel's ambient occlusion averaged over the 3x3 pixels round it, which smooths out the noise that turns its
// samples, and its own contact shadow, which stays sharp.
float2 BlurScreenShadows(FrameData frame, float2 pixel)
{
    Texture2D screenShadows = Textures[frame.ScreenShadows];
    int2 last = int2(frame.TargetSize) - 1;
    float occlusion = 0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            int2 neighbour = clamp(int2(pixel) + int2(x, y), 0, last);
            occlusion += screenShadows.Load(int3(neighbour, 0)).r;
        }
    }

    float contact = screenShadows.Load(int3(pixel, 0)).g;
    return float2(occlusion / 9, contact);
}


// The world (FrameData.Reflection) seen in the direction, blurrier the less glossy the surface, through the image's mips; an
// equirectangular image's columns turn about the up axis and its rows run from straight up to straight down.
float3 SampleReflection(FrameData frame, float3 direction, float glossiness)
{
    Texture2D image = Textures[frame.Reflection];
    uint width;
    uint height;
    uint mips;
    image.GetDimensions(0, width, height, mips);
    float turn = atan2(direction.x, -direction.z) / (2 * 3.14159265) + 0.5;
    float down = acos(clamp(direction.y, -1, 1)) / 3.14159265;
    float mip = (1 - saturate(glossiness)) * (mips - 1);
    float3 seen = image.SampleLevel(Samplers[frame.ReflectionSampler], float2(turn, down), mip).rgb;
    return seen * frame.ReflectionIntensity;
}

// What a surface reflects: the world in the mirror direction, dimmed by the occlusion, where the frame reflects one, else the
// ambient light; and how much of it paint reflects there (none without a world, as before reflections).
float3 ReflectionAt(FrameData frame, Material material, float3 normal, float3 toCamera, float3 ambient, float occlusion, out float sheen)
{
    sheen = 0;
    if (frame.Reflection == None)
    {
        return ambient;
    }

    float3 mirror = reflect(-toCamera, normal);
    sheen = Sheen(material, dot(normal, toCamera));
    return SampleReflection(frame, mirror, material.Glossiness) * occlusion;
}
