// The transparent pass's pixel shader for glass, included after SceneSurface.hlsl and a SurfaceAt function that reads a draw's
// surface through its master's SurfaceOf, as Geometry.hlsl is. The surface's albedo is the glass's tint, and its alpha how much
// paint covers the glass (grime, a painted sign): clear glass shows the lit scene behind it (FrameData.Behind) bent by its
// normal (the material's bend) and blurred (its frost), times its tint, with a Fresnel reflection of the ambient light and the
// probes toward its edges, and the sun's and the lamps' highlights; its paint is lit as the lighting pass lights paint. Its own
// light is seen through the fog before it, and the scene behind keeps its coverage, so the canvas shows through glass as it
// shows through what is behind it.

#pragma once

#include "Shading.hlsl"

// How far a glass's normal bends what is seen through it at a bend of 1, in world units.
static const float BendReach = 0.25;

// How wide the blur of a frost of 1 is, in pixels.
static const float FrostRadius = 12;

// How much light glass reflects head on (Schlick's approximation of the Fresnel factor).
static const float GlassReflectance = 0.04;

// The lit scene behind the pixel, the pixel itself where what lies there stands in front of the glass.
float4 BehindAt(FrameData frame, int2 pixel, int2 fallback, float glassDepth)
{
    int2 size = int2(frame.TargetSize) - 1;
    int3 texel = int3(clamp(pixel, 0, size), 0);
    float depth = Textures[frame.Depth].Load(texel).r;
    if (depth < glassDepth)
    {
        texel = int3(fallback, 0);
    }

    return Textures[frame.Behind].Load(texel);
}

// The scene seen through the glass: moved to where the normal bends the view to, and averaged over a disk the frost widens.
float4 SeeThrough(FrameData frame, VertexOutput input, float3 normal, float bend, float frost)
{
    float3 bent = input.WorldPosition - normal * bend * BendReach;
    float2 bentPixel = ProjectToPixel(frame, bent).xy;
    int2 own = int2(input.Position.xy);
    float radius = saturate(frost) * FrostRadius;
    float4 seen = BehindAt(frame, int2(bentPixel), own, input.Position.z);
    if (radius < 0.5)
    {
        return seen;
    }

    static const float2 Disk[8] = { float2(1, 0), float2(0.71, 0.71), float2(0, 1), float2(-0.71, 0.71), float2(-1, 0), float2(-0.71, -0.71), float2(0, -1), float2(0.71, -0.71) };
    for (uint tap = 0; tap < 8; tap++)
    {
        float2 offset = Disk[tap] * radius * (0.5 + 0.5 * (tap & 1));
        int2 pixel = int2(bentPixel + offset);
        seen += BehindAt(frame, pixel, own, input.Position.z);
    }

    return seen / 9;
}

float4 TransparentMain(VertexOutput input, bool front : SV_IsFrontFace) : SV_Target0
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    if (IsCut(frame, draw, input.WorldPosition))
    {
        discard;
    }

    Material material = LoadMaterial(draw.Material);
    Surface surface = SurfaceAt(input, draw, material);
    float3 normal = normalize(surface.Normal);
    if (!front)
    {
        normal = -normal;
    }

    float3 position = input.WorldPosition;
    float3 toCamera = ToCamera(frame, position);
    float2 pixel = input.Position.xy;
    int3 texel = int3(pixel, 0);

    float3 ambient = frame.Ambient * IndoorAmbient(frame, position) + SampleProbes(frame, position, normal, toCamera);
    float3 toSun = -normalize(frame.SunDirection);
    float sun = 1 - frame.Ambient;
    if (frame.SunShadow != None)
    {
        sun *= SampleShadow(frame, frame.SunShadow, position, normal, 0, true);
    }

    float3 diffuse = ambient + sun * Diffuse(material, dot(normal, toSun));
    float3 specular = sun * Specular(material, normal, toSun, toCamera);
    uint cluster = FindCluster(frame, pixel, position);
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light lamp = LoadLight(frame, lightIndex);
        float3 direction;
        float3 incoming = IncomingLight(frame, lamp, position, normal, texel, false, direction);
        diffuse += incoming * Diffuse(material, dot(normal, direction));
        specular += incoming * Specular(material, normal, direction, toCamera);
    }

    float facing = saturate(dot(normal, toCamera));
    float fresnel = GlassReflectance + (1 - GlassReflectance) * pow(1 - facing, 5);
    float3 reflected = reflect(-toCamera, normal);
    float3 reflection = frame.Ambient * IndoorAmbient(frame, position) + SampleProbes(frame, position, reflected, toCamera);
    float paint = saturate(surface.Albedo.a);
    float3 glassLight = reflection * fresnel + specular;
    float3 paintLight = surface.Albedo.rgb * diffuse + specular;
    float3 own = lerp(glassLight, paintLight, paint) + material.Emissive * material.EmissiveIntensity;
    if (frame.FogDensity > 0)
    {
        float4 fog = MarchFog(frame, pixel, position);
        own = own * fog.a + fog.rgb * paint;
    }

    float bend = material.ModelValues.x;
    float frost = material.ModelValues.y;
    float4 behind = SeeThrough(frame, input, normal, bend, frost);
    float3 through = behind.rgb * surface.Albedo.rgb * (1 - fresnel) * (1 - paint);
    float3 color = MuteOutside(frame, through + own, position);
    float coverage = Textures[frame.Behind].Load(texel).a;
    return float4(color, max(coverage, paint));
}
