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
};

[[vk::push_constant]] ConstantBuffer<LightingConstants> Pass : register(b0, space0);

struct Shadow
{
    float4x4 WorldToAtlas;
    float4 Rect;
    float TexelPerDistance;
    float TexelConstant;
};

static const uint FogSteps = 16;

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

// A small per-pixel offset that spreads the march's banding into fine noise: interleaved gradient noise, which holds still, or
// white noise drawn afresh each frame while an upscaler denoises the frame.
float PixelNoise(FrameData frame, float2 pixel)
{
    if (frame.NoiseFrame != None)
    {
        uint2 texel = uint2(pixel);
        uint hash = Hash(texel.x + Hash(texel.y + Hash(frame.NoiseFrame)));
        return HashToUnit(hash);
    }

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
    float jitter = PixelNoise(frame, pixel);
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
    float2 screenShadows = 1;
    if (frame.RayShadows == None || frame.RayOcclusion == None)
    {
        screenShadows = BlurScreenShadows(frame, pixel);
    }

    float3 toSun = -normalize(frame.SunDirection);
    float sunDiffuse = Diffuse(material, dot(normal, toSun));
    float sunSpecular = Specular(material, normal, toSun, toCamera);
    float sun = 1 - frame.Ambient;
    if (frame.RayShadows != None)
    {
        sun *= RayShadow(frame, texel, ShadowRaySun);
    }
    else
    {
        sun *= screenShadows.g;
        if (sun > 0 && sunDiffuse + sunSpecular > 0 && frame.SunShadow != None)
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
