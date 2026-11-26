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
    // kept, and the frame's number.
    uint Lit;
    uint History;
    uint Integrated;
    uint KeepsHistory;
    uint Number;
};

[[vk::push_constant]] ConstantBuffer<LightingConstants> Pass : register(b0, space0);

RWByteAddressBuffer RWBuffers[] : register(u0, space2);

// How much of the fog's lighting a froxel takes from the frame now, the rest it keeps from the frames before.
static const float FogHistoryBlend = 0.1;

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
