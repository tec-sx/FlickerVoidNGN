// Lights the G-buffer into the HDR target, a thread a pixel: each surface's color shaded by its material's controls
// (Scene.fvshader) and lit by the sun, an even ambient light, and the point and spot lights of the pixel's cluster, shadowed by
// traced rays (ShadowRays.hlsl) while the frame traces them, else through the shadow atlas, and tinted by spot light cookies,
// plus bounced light from irradiance probes (Probes.hlsl), seen through fog lit by the same lights through the shadow atlas. Indoor volumes
// scale the ambient light inside them, and the world outside the cutaway's box is muted. ScreenShadows.hlsl gives each pixel
// its ambient occlusion and contact shadow, unless the frame traces rays: then ShadowRays.hlsl traces the occlusion, and traced
// sun shadows have no need of a contact shadow. Alpha is the paint's coverage, more where its impasto stands thick, for the
// canvas; where nothing was drawn the target is left transparent, for the sky or a stage's background.

#include "DepthTone.hlsl"
#include "Frame.hlsl"
#include "GBuffer.hlsl"
#include "Light.hlsl"
#include "Material.hlsl"
#include "Probes.hlsl"

struct LightingConstants
{
    uint Frame;
    uint Output;
    // The fog volume kernels': the volume lit this frame, the one before's, the integrated volume, whether the one before's is
    // kept, and the frame's number.
    uint Lit;
    uint History;
    uint Integrated;
    uint KeepsHistory;
    uint Number;
};

[[vk::push_constant]] ConstantBuffer<LightingConstants> Pass : register(b0, space0);

RWByteAddressBuffer RWBuffers[] : register(u0, space2);

struct Shadow
{
    float4x4 WorldToAtlas;
    float4 Rect;
    float TexelPerDistance;
    float TexelConstant;
};

// How much of the fog's lighting a froxel takes from the frame now, the rest it keeps from the frames before.
static const float FogHistoryBlend = 0.1;

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

// The light a froxel lit this frame holds: the fog's color times the light scattered at its centre, and its density, mixed with
// the frame before's at the same place in the world, reprojected, unless the history is not kept or the place was out of sight.
[numthreads(8, 8, 4)]
void FogLightMain(uint3 id : SV_DispatchThreadID)
{
    if (any(id >= FogGrid))
    {
        return;
    }

    FrameData frame = LoadFrame(Pass.Frame);
    uint seed = Hash(id.x + Hash(id.y + Hash(id.z + Hash(Pass.Number))));
    float jitter = HashToUnit(seed);
    float2 pixel = (float2(id.xy) + 0.5) / float2(FogGrid.xy) * frame.TargetSize;
    float depth = FogSliceDepth(frame, id.z + jitter);
    float3 direction;
    float3 position = FogPosition(frame, pixel, depth, direction);
    float density = FogDensityAt(frame, position);
    float4 current = float4(0, 0, 0, density);
    if (density > 0)
    {
        current.rgb = FogLight(frame, pixel, position, -direction) * frame.FogColor;
    }

    if (Pass.KeepsHistory != 0)
    {
        float4 clip = mul(float4(position, 1), frame.PreviousViewProjection);
        float2 ndc = clip.xy / clip.w;
        float2 uv = float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5);
        float slice = FogSliceAt(frame, dot(float4(position, 1), frame.PreviousDepthAxis));
        bool inside = clip.w > 0 && all(uv >= 0) && all(uv <= 1) && slice > 0 && slice < FogGrid.z;
        if (inside)
        {
            float3 place = float3(uv * FogGrid.xy, slice) - 0.5;
            float4 previous = SampleFogCells(Buffers[Pass.History], place);
            current = lerp(previous, current, FogHistoryBlend);
        }
    }

    RWBuffers[Pass.Lit].Store<float4>(((id.z * FogGrid.y + id.y) * FogGrid.x + id.x) * 16, current);
}

// Integrates a column of froxels front to back, so each slice holds the light the fog scatters toward the camera up to its far
// boundary and the share of a surface's light that gets through (as the surface's own light is seen through it).
[numthreads(8, 8, 1)]
void FogIntegrateMain(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= FogGrid.xy))
    {
        return;
    }

    FrameData frame = LoadFrame(Pass.Frame);
    float2 pixel = (float2(id.xy) + 0.5) / float2(FogGrid.xy) * frame.TargetSize;
    float3 direction;
    FogPosition(frame, pixel, FogSliceDepth(frame, 0), direction);
    float along = dot(direction, frame.DepthAxis.xyz);
    ByteAddressBuffer lit = Buffers[Pass.Lit];
    float3 scattered = 0;
    float transmittance = 1;
    for (uint slice = 0; slice < FogGrid.z; slice++)
    {
        float4 cell = LoadFogCell(lit, int3(id.xy, slice));
        float stepLength = (FogSliceDepth(frame, slice + 1) - FogSliceDepth(frame, slice)) / along;
        float stepTransmittance = exp(-cell.a * stepLength);
        scattered += transmittance * cell.rgb * (1 - stepTransmittance);
        transmittance *= stepTransmittance;
        uint index = (slice * FogGrid.y + id.y) * FogGrid.x + id.x;
        RWBuffers[Pass.Integrated].Store<float4>(index * 16, float4(scattered, transmittance));
    }
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

[numthreads(8, 8, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    RWTexture2D<float4> output = RWTextures[Pass.Output];
    int3 texel = int3(id.xy, 0);
    float depth = Textures[frame.Depth].Load(texel).r;
    if (depth >= 1)
    {
        output[id.xy] = 0;
        return;
    }

    float2 pixel = float2(id.xy) + 0.5;
    float3 worldPosition = WorldAtPixel(frame, pixel, depth);
    float4 albedo = Textures[frame.GBufferAlbedo].Load(texel);
    float2 folded = Textures[frame.GBufferNormal].Load(texel).xy;
    float3 normal = DecodeNormal(folded);
    float4 materialBytes = Textures[frame.GBufferMaterial].Load(texel);
    SurfaceMaterial surface = DecodeMaterial(materialBytes);
    Material material = LoadMaterial(surface.Material);
    float3 toCamera = ToCamera(frame, worldPosition);
    float2 screenShadows = 1;
    if (frame.RayShadows == None || frame.RayOcclusion == None)
    {
        screenShadows = BlurScreenShadows(frame, pixel);
    }

    float3 toSun = -normalize(frame.SunDirection);
    float3 sunDiffuse = Diffuse(material, dot(normal, toSun));
    float sunSpecular = Specular(material, normal, toSun, toCamera);
    float sun = 1 - frame.Ambient;
    if (frame.RayShadows != None)
    {
        sun *= RayShadow(frame, texel, ShadowRaySun);
    }
    else
    {
        sun *= screenShadows.g;
        if (sun > 0 && (any(sunDiffuse > 0) || sunSpecular > 0) && frame.SunShadow != None)
        {
            sun *= SampleShadow(frame, frame.SunShadow, worldPosition, normal, 0, true);
        }
    }

    float occlusion = screenShadows.r;
    if (frame.RayOcclusion != None)
    {
        float reached = Textures[frame.RayOcclusion].Load(texel).r;
        occlusion = 1 - frame.AmbientOcclusion * (1 - reached);
    }

    float3 ambient = frame.Ambient * IndoorAmbient(frame, worldPosition) + SampleProbes(frame, worldPosition, normal, toCamera);
    float3 sunLight = sun * frame.SunLight;
    float3 direct = sunLight * sunDiffuse;
    float3 specular = sunLight * sunSpecular;
    ShadeClusterLights(frame, material, pixel, worldPosition, normal, toCamera, direct, specular);
    // A near object stands in a cast shadow: its direct light goes by its object depth, its ambient light stays.
    float objectDepth = Textures[frame.GBufferObjectDepth].Load(texel).r;
    float keep = NearShadow(frame, objectDepth);
    float3 diffuse = ambient * occlusion + direct * keep;
    specular *= keep;
    float3 color = ShadeSurface(material, albedo.rgb, diffuse, specular, ambient);

    // Painted touches: a tint where little light falls, a rim toward the silhouette, and the material's own glow.
    float lit = saturate(max(diffuse.r, max(diffuse.g, diffuse.b)));
    color += albedo.rgb * material.ShadowTint * (1 - lit);
    float facing = saturate(dot(normal, toCamera));
    color += material.RimColor * (material.RimStrength * pow(1 - facing, material.RimPower));
    color += material.Emissive * material.EmissiveIntensity;
    if (frame.FogVolume != None)
    {
        float depthAlong = dot(float4(worldPosition, 1), frame.DepthAxis);
        float4 fog = SampleFog(frame, pixel, depthAlong);
        color = color * fog.a + fog.rgb;
    }

    color = MuteOutside(frame, color, worldPosition);
    if ((surface.Flags & FlagGreyed) != 0)
    {
        color = dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    output[id.xy] = float4(color, albedo.a + surface.Thickness * albedo.a);
}
