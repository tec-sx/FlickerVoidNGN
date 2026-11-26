// Lights the G-buffer into the HDR target, a thread a pixel: each surface's color shaded by its material's controls
// (Scene.fvshader) and lit by the sun, an even ambient light, and the point and spot lights of the pixel's cluster, shadowed by
// traced rays (ShadowRays.hlsl) while the frame traces them, else through the shadow atlas, and tinted by spot light cookies,
// plus bounced light from irradiance probes (Probes.hlsl), seen through fog lit by the same lights through the shadow atlas. Indoor volumes
// scale the ambient light inside them, and the world outside the cutaway's box is muted. ScreenShadows.hlsl gives each pixel
// its ambient occlusion and contact shadow, unless the frame traces rays: then ShadowRays.hlsl traces the occlusion, and traced
// sun shadows have no need of a contact shadow. Alpha is the paint's coverage, more where its impasto stands thick, for the
// canvas; where nothing was drawn the target is left transparent, for the sky or a stage's background.

#include "Shading.hlsl"

struct LightingConstants
{
    uint Frame;
    uint Output;
};

[[vk::push_constant]] ConstantBuffer<LightingConstants> Pass : register(b0, space0);

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
    float3 diffuse = ambient * occlusion + sunLight * sunDiffuse;
    float3 specular = sunLight * sunSpecular;
    ShadeClusterLights(frame, material, pixel, worldPosition, normal, toCamera, diffuse, specular);
    float sheen;
    float3 reflection = ReflectionAt(frame, material, normal, toCamera, ambient, occlusion, sheen);
    float3 color = ShadeSurface(material, albedo.rgb, diffuse, specular, reflection, sheen);

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
