// Lights the G-buffer into the HDR target, a thread a pixel: each surface's color shaded by its material's controls
// (Scene.fvshader) and lit by the sun, an even ambient light, and the point and spot lights of the pixel's cluster, shadowed
// through the shadow atlas and tinted by spot light cookies, plus bounced light from irradiance probes, seen through fog lit by
// the same lights. Indoor volumes scale the ambient light inside them, and the world outside the cutaway's box is muted.
// ScreenShadows.hlsl gives each pixel its ambient occlusion and contact shadow. Alpha is the paint's coverage, more where its
// impasto stands thick, for the canvas; where nothing was drawn the target is left transparent, for the sky or a stage's
// background.

#include "Frame.hlsl"
#include "GBuffer.hlsl"
#include "Material.hlsl"

struct LightingConstants
{
    uint Frame;
    uint Output;
};

[[vk::push_constant]] ConstantBuffer<LightingConstants> Pass : register(b0, space0);

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
};

struct Shadow
{
    float4x4 WorldToAtlas;
    float4 Rect;
    float TexelPerDistance;
    float TexelConstant;
};

static const uint FogSteps = 16;

static const uint ProbeSize = 48;

static const uint LightSize = 80;

static const uint ShadowSize = 96;

static const float AtlasSize = 4096;

// Normal offset in texels and depth bias that keep surfaces from shadowing themselves.
static const float NormalBias = 1.5;

static const float DepthBias = 0.0003;

// The cluster grid of LightClusters.
static const uint TilesX = 16;

static const uint TilesY = 9;

static const uint Slices = 24;

// The share of light a surface takes at the cosine between its normal and the light: wrapped round past the terminator,
// then pulled toward a hard step as the ramp softness falls (1 is plain wrapped diffuse light).
float Diffuse(Material material, float cosine)
{
    float wrapped = saturate((cosine + material.Wrap) / (1 + material.Wrap));
    float halfWidth = max(0.5 * material.RampSoftness, 0.001);
    float stepped = smoothstep(0.5 - halfWidth, 0.5 + halfWidth, wrapped);
    return lerp(stepped, wrapped, material.RampSoftness);
}

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

static const uint IndoorSize = 32;

// The share of the mood's ambient light at a point: the least of the indoor volumes it stands in, else all of it.
float IndoorAmbient(FrameData frame, float3 worldPosition)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    float ambient = 1;
    for (uint index = 0; index < frame.IndoorCount; index++)
    {
        uint offset = frame.IndoorsOffset + index * IndoorSize;
        float3 boxMin = buffer.Load<float3>(offset);
        float boxAmbient = buffer.Load<float>(offset + 12);
        float3 boxMax = buffer.Load<float3>(offset + 16);
        if (IsInBox(worldPosition, boxMin, boxMax))
        {
            ambient = min(ambient, boxAmbient);
        }
    }

    return ambient;
}

// Greys and darkens the world outside the cutaway's box by its muting, so the room it looks into stands out.
float3 MuteOutside(FrameData frame, float3 color, float3 worldPosition)
{
    if (frame.Cut == 0 || frame.CutMuted <= 0 || IsInBox(worldPosition, frame.CutMin, frame.CutMax))
    {
        return color;
    }

    float grey = dot(color, float3(0.2126, 0.7152, 0.0722));
    float3 muted = lerp(color, grey, frame.CutMuted);
    return muted * (1 - (0.5 * frame.CutMuted));
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

// The cookie's color where the light's ray through the point crosses it, the outer cone filling the image.
float3 SampleCookie(FrameData frame, Light light, float3 fromLight)
{
    float3 up = cross(light.Right, light.Direction);
    float3 local = float3(dot(fromLight, light.Right), dot(fromLight, up), dot(fromLight, light.Direction));
    float tanOuter = sqrt(saturate(1 - light.CosOuter * light.CosOuter)) / light.CosOuter;
    float2 spread = local.xy / (local.z * tanOuter);
    float2 uv = float2(0.5 + 0.5 * spread.x, 0.5 - 0.5 * spread.y);
    // Level 0: the light loop isn't uniform across pixels, so there are no derivatives to pick a mip with.
    return Textures[light.Cookie].SampleLevel(Samplers[frame.ClampSampler], uv, 0).rgb;
}

// The light arriving at a point from the light, with inverse-square falloff softened near the light and windowed to reach
// zero at its range, the spot cone, the cookie and the shadow; direction points toward the light. A surface's normal
// offsets the shadow lookup, and a point in fog has none.
float3 IncomingLight(FrameData frame, Light light, float3 worldPosition, float3 normal, bool soft, out float3 direction)
{
    float3 toLight = light.Position - worldPosition;
    float distanceSquared = dot(toLight, toLight);
    float distance = sqrt(distanceSquared);
    direction = toLight / max(distance, 1e-4);
    float ratio = distanceSquared / (light.Range * light.Range);
    float window = saturate(1 - ratio * ratio);
    float falloff = window * window / (distanceSquared + 1);
    float cone = smoothstep(light.CosOuter, light.CosInner, dot(-direction, light.Direction));
    float3 color = light.Color * (falloff * cone);
    if (all(color <= 0))
    {
        return 0;
    }

    if (light.Cookie != None)
    {
        color *= SampleCookie(frame, light, -direction);
    }

    if (light.Shadow != None)
    {
        uint shadow = light.Shadow;
        if (light.CosOuter < -1)
        {
            shadow += PointFace(-toLight);
        }

        color *= SampleShadow(frame, shadow, worldPosition, normal, distance, soft);
    }

    return color;
}

// Adds the diffuse and specular light of the pixel's cluster lights.
void ShadeClusterLights(FrameData frame, Material material, float2 pixel, float3 worldPosition, float3 normal, float3 toCamera, inout float3 diffuse, inout float3 specular)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    uint cluster = FindCluster(frame, pixel, worldPosition);
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light clusterLight = buffer.Load<Light>(FrameDataSize + lightIndex * LightSize);
        float3 direction;
        float3 incoming = IncomingLight(frame, clusterLight, worldPosition, normal, true, direction);
        float cosine = dot(normal, direction);
        diffuse += incoming * Diffuse(material, cosine);
        specular += incoming * Specular(material, normal, direction, toCamera);
    }
}

// A probe's spherical harmonics turned into the irradiance (over pi) a surface facing the normal gets.
float3 EvaluateProbe(ByteAddressBuffer probes, uint index, float3 normal)
{
    uint offset = index * ProbeSize;
    float4 basis = float4(1, normal);
    float red = dot(probes.Load<float4>(offset), basis);
    float green = dot(probes.Load<float4>(offset + 16), basis);
    float blue = dot(probes.Load<float4>(offset + 32), basis);
    return max(float3(red, green, blue), 0);
}

// The irradiance blended from the eight probes round the point, or nothing outside the volume.
float3 SampleProbes(FrameData frame, float3 worldPosition, float3 normal)
{
    if (frame.Probes == None)
    {
        return 0;
    }

    float3 local = (worldPosition - frame.ProbeMin) / frame.ProbeSize;
    if (any(local < 0) || any(local > 1))
    {
        return 0;
    }

    ByteAddressBuffer probes = Buffers[frame.Probes];
    uint3 last = frame.ProbeCounts - 1;
    float3 grid = clamp(local * float3(frame.ProbeCounts) - 0.5, 0, float3(last));
    uint3 low = min(uint3(grid), last);
    uint3 high = min(low + 1, last);
    float3 blend = grid - float3(low);
    float3 irradiance = 0;
    for (uint corner = 0; corner < 8; corner++)
    {
        uint3 bits = uint3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
        uint3 cell = low + bits * (high - low);
        float3 weights = lerp(1 - blend, blend, float3(bits));
        uint index = (cell.z * frame.ProbeCounts.y + cell.y) * frame.ProbeCounts.x + cell.x;
        irradiance += weights.x * weights.y * weights.z * EvaluateProbe(probes, index, normal);
    }

    return irradiance;
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
    float3 light = frame.Ambient * IndoorAmbient(frame, position) + SampleProbes(frame, position, 0);
    float3 sunDirection = normalize(frame.SunDirection);
    float sun = (1 - frame.Ambient) * Phase(dot(sunDirection, towardCamera), frame.FogAnisotropy);
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
        Light clusterLight = buffer.Load<Light>(FrameDataSize + lightIndex * LightSize);
        float3 direction;
        float3 incoming = IncomingLight(frame, clusterLight, position, 0, false, direction);
        light += incoming * Phase(dot(-direction, towardCamera), frame.FogAnisotropy);
    }

    return light;
}

// A small per-pixel offset that spreads the march's banding into fine noise (interleaved gradient noise).
float PixelNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

// Fog density at a point: full at and below the fog height, thinning exponentially above it.
float FogDensityAt(FrameData frame, float3 position)
{
    float above = max(position.y - frame.FogHeight, 0);
    return frame.FogDensity * exp(-above * frame.FogHeightFalloff);
}

// Marches from the near plane to the surface in even steps, integrating each step exactly for light and density that hold
// still across it. Returns the light the fog scatters toward the camera and the share of the surface's light it lets through.
float4 MarchFog(FrameData frame, float2 pixel, float3 surface)
{
    float3 forward = frame.DepthAxis.xyz;
    float3 direction = forward;
    if (frame.LogarithmicSlices != 0)
    {
        direction = normalize(surface - frame.CameraPosition);
    }

    float depth = dot(float4(surface, 1), frame.DepthAxis);
    float rayLength = max(depth - frame.NearPlane, 0) / dot(direction, forward);
    float stepLength = rayLength / FogSteps;
    float jitter = PixelNoise(pixel);
    float3 scattered = 0;
    float transmittance = 1;
    for (uint index = 0; index < FogSteps; index++)
    {
        float travelled = (index + jitter) * stepLength;
        float3 position = surface - direction * (rayLength - travelled);
        float stepTransmittance = exp(-FogDensityAt(frame, position) * stepLength);
        if (stepTransmittance >= 1)
        {
            continue;
        }

        float3 light = FogLight(frame, pixel, position, -direction);
        scattered += transmittance * light * (1 - stepTransmittance);
        transmittance *= stepTransmittance;
    }

    return float4(scattered * frame.FogColor, transmittance);
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
    float2 screenShadows = BlurScreenShadows(frame, pixel);
    float3 toSun = -normalize(frame.SunDirection);
    float sunDiffuse = Diffuse(material, dot(normal, toSun));
    float sunSpecular = Specular(material, normal, toSun, toCamera);
    float sun = (1 - frame.Ambient) * screenShadows.g;
    if (sun > 0 && sunDiffuse + sunSpecular > 0 && frame.SunShadow != None)
    {
        sun *= SampleShadow(frame, frame.SunShadow, worldPosition, normal, 0, true);
    }

    float3 ambient = frame.Ambient * IndoorAmbient(frame, worldPosition) + SampleProbes(frame, worldPosition, normal);
    float3 diffuse = ambient * screenShadows.r + sun * sunDiffuse;
    float3 specular = sun * sunSpecular;
    ShadeClusterLights(frame, material, pixel, worldPosition, normal, toCamera, diffuse, specular);
    float3 color = albedo.rgb * diffuse + specular;

    // Painted touches: a tint where little light falls, a rim toward the silhouette, and the material's own glow.
    float lit = saturate(max(diffuse.r, max(diffuse.g, diffuse.b)));
    color += albedo.rgb * material.ShadowTint * (1 - lit);
    float facing = saturate(dot(normal, toCamera));
    color += material.RimColor * (material.RimStrength * pow(1 - facing, material.RimPower));
    color += material.Emissive * material.EmissiveIntensity;
    if (frame.FogDensity > 0)
    {
        float4 fog = MarchFog(frame, pixel, worldPosition);
        color = color * fog.a + fog.rgb;
    }

    color = MuteOutside(frame, color, worldPosition);
    if ((surface.Flags & FlagGreyed) != 0)
    {
        color = dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    output[id.xy] = float4(color, albedo.a + surface.Thickness * albedo.a);
}
