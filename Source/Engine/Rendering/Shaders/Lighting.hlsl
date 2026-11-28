// Lights the G-buffer into the HDR target, a thread a pixel: each surface's color shaded by its material's controls
// (Scene.fvshader) and lit by the sun, an even ambient light, and the point and spot lights of the pixel's cluster, shadowed by
// traced rays (ShadowRays.hlsl) while the frame traces them, else through the shadow atlas, and tinted by spot light cookies,
// plus bounced light from irradiance probes (Probes.hlsl), seen through fog lit by the same lights through the shadow atlas. Indoor volumes
// scale the ambient light inside them, and the world outside the cutaway's box is muted. ScreenShadows.hlsl gives each pixel
// its ambient occlusion and contact shadow, unless the frame traces rays: then ShadowRays.hlsl traces the occlusion, and traced
// sun shadows have no need of a contact shadow. Alpha is the paint's coverage, more where its impasto stands thick, for the
// canvas; where nothing was drawn the target is left transparent, for the sky or a stage's background.

#include "DepthTone.hlsl"
#include "Shading.hlsl"

struct LightingConstants
{
    uint Frame;
    uint Output;
    // The fog volume kernels': the volume lit this frame, the one before's, the integrated volume, whether the one before's is
    // kept, the frame's number, and the fog's own data: its noise and fog volumes (FogVolumePass.cs, GpuFog).
    uint Lit;
    uint History;
    uint Integrated;
    uint KeepsHistory;
    uint Number;
    uint Fog;
    // The renderer's time in seconds (RenderContext.Time), which rain ripples move by.
    float Time;
    // Not zero draws the flat look in place of the lit scene (SceneRenderer.FlatLook).
    uint Flat;
};

[[vk::push_constant]] ConstantBuffer<LightingConstants> Pass : register(b0, space0);

RWByteAddressBuffer RWBuffers[] : register(u0, space2);

// How much of the fog's lighting a froxel takes from the frame now, the rest it keeps from the frames before.
static const float FogHistoryBlend = 0.1;

// The fog's own data (GpuFogHeader.cs, then GpuFogVolume.cs records): the bytes of its header and of a fog volume.
static const uint FogHeaderSize = 32;
static const uint FogVolumeSize = 96;

// How far the noise moves a fog's density at a share of 1, each way.
static const float FogNoiseReach = 2;

// A value between 0 and 1 for a corner of the noise's grid.
float FogNoiseCorner(int3 corner)
{
    return HashToUnit(Hash(uint(corner.x) + Hash(uint(corner.y) + Hash(uint(corner.z)))));
}

// Smooth value noise between 0 and 1 over a grid of unit cells.
float FogValueNoise(float3 position)
{
    float3 cell = floor(position);
    float3 fraction = position - cell;
    float3 blend = fraction * fraction * (3 - 2 * fraction);
    int3 corner = int3(cell);
    float x00 = lerp(FogNoiseCorner(corner), FogNoiseCorner(corner + int3(1, 0, 0)), blend.x);
    float x10 = lerp(FogNoiseCorner(corner + int3(0, 1, 0)), FogNoiseCorner(corner + int3(1, 1, 0)), blend.x);
    float x01 = lerp(FogNoiseCorner(corner + int3(0, 0, 1)), FogNoiseCorner(corner + int3(1, 0, 1)), blend.x);
    float x11 = lerp(FogNoiseCorner(corner + int3(0, 1, 1)), FogNoiseCorner(corner + int3(1, 1, 1)), blend.x);
    float y0 = lerp(x00, x10, blend.y);
    float y1 = lerp(x01, x11, blend.y);
    return lerp(y0, y1, blend.z);
}

// What a share of noise does to a density at a point: 1 where the noise is middling, more or less round it, never below 0.
float FogNoiseFactor(float noise, float share)
{
    return max(0, 1 + share * (noise * 2 - 1) * FogNoiseReach);
}

// The density a fog volume adds at a point: its own, thinned over its soft edge and upward by its falloff.
float FogVolumeDensity(ByteAddressBuffer fog, uint offset, float3 position, out float3 color, out float noiseShare)
{
    float4 centerDensity = fog.Load<float4>(offset);
    float4 axisX = fog.Load<float4>(offset + 16);
    float4 axisY = fog.Load<float4>(offset + 32);
    float4 axisZ = fog.Load<float4>(offset + 48);
    float4 colorSoftness = fog.Load<float4>(offset + 64);
    float4 falloffNoiseShape = fog.Load<float4>(offset + 80);
    color = colorSoftness.rgb;
    noiseShare = falloffNoiseShape.y;
    float3 offsetFromCenter = position - centerDensity.xyz;
    float3 local = float3(dot(offsetFromCenter, axisX.xyz), dot(offsetFromCenter, axisY.xyz), dot(offsetFromCenter, axisZ.xyz));
    float3 halfSize = max(float3(axisX.w, axisY.w, axisZ.w), 1e-4);
    float3 scaled = local / halfSize;
    float edge = max(abs(scaled.x), max(abs(scaled.y), abs(scaled.z)));
    if (asuint(falloffNoiseShape.z) == 1)
    {
        edge = length(scaled);
    }

    if (edge >= 1)
    {
        return 0;
    }

    float softness = colorSoftness.a;
    float fade = 1;
    if (softness > 0)
    {
        fade = saturate((1 - edge) / softness);
    }

    float above = local.y + halfSize.y;
    return centerDensity.w * fade * exp(-above * falloffNoiseShape.x);
}

// The fog at a point: its colour, the mood's and its volumes' weighted by their density, and its density, broken up by the
// drifting noise as the mood and each volume say.
float4 FogMediumAt(FrameData frame, uint fogBuffer, float3 position)
{
    ByteAddressBuffer fog = Buffers[fogBuffer];
    uint count = fog.Load(0);
    float moodNoise = fog.Load<float>(4);
    float scale = max(fog.Load<float>(8), 1e-3);
    float3 drift = fog.Load<float3>(16);
    float noise = 0.5;
    bool needsNoise = moodNoise > 0;
    for (uint check = 0; check < count && !needsNoise; check++)
    {
        needsNoise = fog.Load<float>(FogHeaderSize + check * FogVolumeSize + 84) > 0;
    }

    if (needsNoise)
    {
        float3 place = (position - drift) / scale;
        noise = 0.65 * FogValueNoise(place) + 0.35 * FogValueNoise(place * 2.03 + 17.1);
    }

    float moodDensity = FogDensityAt(frame, position) * FogNoiseFactor(noise, moodNoise);
    float density = moodDensity;
    float3 colorSum = frame.FogColor * moodDensity;
    for (uint index = 0; index < count; index++)
    {
        float3 color;
        float noiseShare;
        float volumeDensity = FogVolumeDensity(fog, FogHeaderSize + index * FogVolumeSize, position, color, noiseShare);
        volumeDensity *= FogNoiseFactor(noise, noiseShare);
        density += volumeDensity;
        colorSum += color * volumeDensity;
    }

    float3 medium = frame.FogColor;
    if (density > 0)
    {
        medium = colorSum / density;
    }

    return float4(medium, density);
}

// Where in its slice a froxel samples this frame, from 0 to 1: interleaved gradient noise across the grid, so neighbouring
// froxels spread their samples evenly over the slice rather than at random, turned by the golden ratio each frame, so a froxel
// covers its slice evenly over the frames its history keeps.
float FogJitter(uint3 id, uint number)
{
    float2 place = float2(id.xy) + float2(5.588238, 3.137254) * id.z;
    float gradient = frac(52.9829189 * frac(dot(place, float2(0.06711056, 0.00583715))));
    return frac(gradient + number * 0.618034);
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
    float jitter = FogJitter(id, Pass.Number);
    float2 pixel = (float2(id.xy) + 0.5) / float2(FogGrid.xy) * frame.TargetSize;
    float depth = FogSliceDepth(frame, id.z + jitter);
    float3 direction;
    float3 position = FogPosition(frame, pixel, depth, direction);
    float4 medium = FogMediumAt(frame, Pass.Fog, position);
    float density = medium.a;
    float4 current = float4(0, 0, 0, density);
    if (density > 0)
    {
        current.rgb = FogLight(frame, pixel, position, -direction) * medium.rgb;
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

// A froxel's light and density blurred with its eight neighbours in its slice by a 3x3 tent, so what's left of the jitter's
// noise between neighbouring froxels doesn't show as a mottle; the history keeps the unblurred froxels.
float4 LoadFogCellBlurred(ByteAddressBuffer volume, int3 cell)
{
    float4 sum = 0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float weight = (2 - abs(x)) * (2 - abs(y));
            sum += LoadFogCell(volume, cell + int3(x, y, 0)) * weight;
        }
    }

    return sum / 16;
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
        float4 cell = LoadFogCellBlurred(lit, int3(id.xy, slice));
        float stepLength = (FogSliceDepth(frame, slice + 1) - FogSliceDepth(frame, slice)) / along;
        float stepTransmittance = exp(-cell.a * stepLength);
        scattered += transmittance * cell.rgb * (1 - stepTransmittance);
        transmittance *= stepTransmittance;
        uint index = (slice * FogGrid.y + id.y) * FogGrid.x + id.x;
        RWBuffers[Pass.Integrated].Store<float4>(index * 16, float4(scattered, transmittance));
    }
}

// Puddles lie where the ground is near flat: patches of slow noise over the ground, more of them the harder it rains.
static const float PuddleScale = 0.6;
static const float PuddleFlatness = 0.8;

// Rain drops ring outward over wet ground: one ring at a time in each cell of this size, each living a fraction of a second.
static const float RippleCell = 0.5;
static const float RippleRate = 1.6;
static const float RippleRadius = 0.15;
static const float RippleWidth = 0.02;

// How the rain's rings tilt the ground's normal at a point: the rings of its own cell and the cells round it, those that rain
// has started, each widening and fading over its life.
float2 RippleTilt(float2 ground, float rain, float time)
{
    float2 tilt = 0;
    int2 home = int2(floor(ground / RippleCell));
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            int2 cell = home + int2(x, y);
            uint hash = Hash(uint(cell.x) * 73856093u ^ uint(cell.y) * 19349663u);
            float2 offset = float2(HashToUnit(hash), HashToUnit(Hash(hash)));
            float phase = HashToUnit(Hash(hash + 1u));
            float cycle = time * RippleRate + phase;
            uint drop = uint(floor(cycle));
            float chance = HashToUnit(Hash(hash ^ drop));
            if (chance > rain)
            {
                continue;
            }

            float life = cycle - floor(cycle);
            float2 center = (float2(cell) + 0.1 + 0.8 * offset) * RippleCell;
            float2 away = ground - center;
            float reach = length(away);
            float ring = (reach - life * RippleRadius) / RippleWidth;
            float strength = exp(-ring * ring) * (1 - life);
            tilt += away / max(reach, 1e-4) * strength;
        }
    }

    return tilt;
}

// Rain wets a surface outdoors, less where it faces down: wet paint darkens by almost half and its highlight grows stronger and
// tighter. On near flat ground it gathers in puddles, darker still, glossy as a mirror and flat, and drops ring over it.
void Wet(FrameData frame, float3 worldPosition, inout float3 normal, inout float3 albedo, inout Material material)
{
    if (frame.Rain <= 0 || IsIndoors(frame, worldPosition))
    {
        return;
    }

    float rain = saturate(frame.Rain);
    float facing = lerp(0.4, 1, saturate(normal.y * 0.5 + 0.5));
    float wet = rain * facing;
    albedo *= 1 - 0.45 * wet;
    material.Specular = lerp(material.Specular, max(material.Specular, 1), wet);
    material.Glossiness = lerp(material.Glossiness, max(material.Glossiness, 0.7), wet);
    float flat = saturate((normal.y - PuddleFlatness) / (1 - PuddleFlatness));
    if (flat <= 0)
    {
        return;
    }

    float2 ground = worldPosition.xz;
    float3 place = float3(ground * PuddleScale, 0);
    float noise = 0.65 * FogValueNoise(place) + 0.35 * FogValueNoise(place * 2.7 + 5.3);
    float threshold = lerp(0.8, 0.52, rain);
    float puddle = smoothstep(threshold, threshold + 0.04, noise) * flat;
    albedo *= 1 - 0.6 * puddle;
    material.Glossiness = lerp(material.Glossiness, 0.97, puddle);
    float2 tilt = RippleTilt(ground, rain, Pass.Time) * lerp(0.04, 0.35, puddle) * flat;
    float3 level = normalize(lerp(normal, float3(0, 1, 0), puddle));
    normal = normalize(level + float3(tilt.x, 0, tilt.y));
}

// The flat look's tones: a face lit only by the way it faces, warm and dark as the editor's theme, and the thin lines between faces.
static const float3 FlatTop = float3(0.100, 0.080, 0.063);
static const float3 FlatSideX = float3(0.032, 0.025, 0.020);
static const float3 FlatSideZ = float3(0.058, 0.046, 0.036);
static const float3 FlatUnder = float3(0.018, 0.014, 0.011);
static const float3 FlatLine = float3(0.55, 0.47, 0.39);
// Faces turned from each other by more than this (its cosine, 32 degrees) have a line between them; a round pillar's sides don't.
static const float FlatCrease = 0.85;
// A solid drawn without a material is this grey (SolidModel.Color); a part's own colour shades its tone from it.
static const float FlatGrey = 0.6;

// Whether the surface at a neighbouring pixel is another face than the one at the centre: it faces another way, it lies off the
// centre's plane, or it is another object's. `apart` says the two do not touch (one stands in front of the other).
bool FlatDiffers(FrameData frame, int2 texel, int2 offset, float depth, float3 worldPosition, float3 normal, float objectDepth, out bool apart, out float otherDepth)
{
    int2 other = clamp(texel + offset, int2(0, 0), int2(frame.TargetSize) - 1);
    otherDepth = Textures[frame.Depth].Load(int3(other, 0)).r;
    apart = true;
    if (otherDepth >= 1)
    {
        return true;
    }

    float3 otherPosition = WorldAtPixel(frame, float2(other) + 0.5, otherDepth);
    float3 otherNormal = DecodeNormal(Textures[frame.GBufferNormal].Load(int3(other, 0)).xy);
    float3 step = otherPosition - worldPosition;
    float off = abs(dot(step, normal));
    float otherOff = abs(dot(step, otherNormal));
    float reach = length(step);
    apart = min(off, otherOff) > 0.4 * reach + 0.002;
    if (apart || dot(normal, otherNormal) < FlatCrease)
    {
        return true;
    }

    // Another object's: an object's depth is its centre's, or for a large one each pixel's own, so within one object it moves
    // from pixel to pixel by no more than the pixels' own depths do, and by how coarsely its half float keeps a depth that far.
    float otherObject = Textures[frame.GBufferObjectDepth].Load(int3(other, 0)).r;
    float along = dot(float4(worldPosition, 1), frame.DepthAxis);
    float otherAlong = dot(float4(otherPosition, 1), frame.DepthAxis);
    float coarse = 0.002 * max(abs(objectDepth), abs(otherObject));
    return abs((otherObject - otherAlong) - (objectDepth - along)) > abs(otherAlong - along) + coarse + 0.002;
}

// The flat look of a pixel: its face's tone, shaded by the part's own colour, or a line where it borders another face. A border
// between faces that touch is drawn on one side only and a silhouette on the nearer face, so lines stay a pixel thin.
float3 FlatLook(FrameData frame, int2 texel, float depth, float3 worldPosition, float3 normal, float3 albedo)
{
    float3 side = lerp(FlatSideZ, FlatSideX, saturate(abs(normal.x) / max(abs(normal.x) + abs(normal.z), 1e-4)));
    float3 tone = lerp(side, FlatTop, saturate(normal.y));
    tone = lerp(tone, FlatUnder, saturate(-normal.y));
    float shade = dot(albedo, float3(0.2126, 0.7152, 0.0722)) / FlatGrey;
    tone *= clamp(shade, 0.5, 1.3);

    float objectDepth = Textures[frame.GBufferObjectDepth].Load(int3(texel, 0)).r;
    static const int2 Offsets[4] = { int2(1, 0), int2(0, 1), int2(-1, 0), int2(0, -1) };
    bool onLine = false;
    for (int index = 0; index < 4; index++)
    {
        bool apart;
        float otherDepth;
        if (!FlatDiffers(frame, texel, Offsets[index], depth, worldPosition, normal, objectDepth, apart, otherDepth))
        {
            continue;
        }

        if (apart)
        {
            onLine = onLine || depth < otherDepth;
        }
        else
        {
            onLine = onLine || index < 2;
        }
    }

    if (onLine)
    {
        return lerp(tone, FlatLine, 0.75);
    }

    return tone;
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
    if (Pass.Flat != 0)
    {
        float3 flat = FlatLook(frame, texel, depth, worldPosition, normal, albedo.rgb);
        output[id.xy] = float4(flat, albedo.a + surface.Thickness * albedo.a);
        return;
    }

    Material material = LoadMaterial(surface.Material);
    Wet(frame, worldPosition, normal, albedo.rgb, material);
    float3 toCamera = ToCamera(frame, worldPosition);
    float2 screenShadows = 1;
    if (frame.RayShadows == None || frame.RayOcclusion == None)
    {
        screenShadows = BlurScreenShadows(frame, pixel);
    }

    float3 toSun = -normalize(frame.SunDirection);
    float3 sunDiffuse = Diffuse(material, dot(normal, toSun));
    float sunSpecular = Specular(material, normal, toSun, toCamera);
    float shadow = 1;
    if (frame.RayShadows != None)
    {
        shadow = RayShadow(frame, texel, ShadowRaySun);
    }
    else
    {
        shadow = screenShadows.g;
        if (shadow > 0 && (any(sunDiffuse > 0) || sunSpecular > 0) && frame.SunShadow != None)
        {
            shadow *= SampleShadow(frame, frame.SunShadow, worldPosition, normal, 0);
        }
    }

    float sun = (1 - frame.Ambient) * lerp(1, shadow, frame.ShadowDarkness);

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
    float3 diffuse = (ambient * occlusion + direct * keep) * lerp(1, frame.ShadowColor, 1 - shadow);
    specular *= keep;
    float sheen;
    float3 reflection = ReflectionAt(frame, material, normal, toCamera, ambient, occlusion, sheen);
    float3 color = ShadeSurface(material, albedo.rgb, diffuse, specular, reflection, sheen);

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
