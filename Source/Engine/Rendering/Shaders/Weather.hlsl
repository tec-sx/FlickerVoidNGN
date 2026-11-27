// The weather's particles, drawn over the lit scene in one instanced draw each, added to it and hidden behind the scene's depth.
// Nothing is stored: a particle's place comes from its number and the time, falling or drifting along its velocity and wrapping
// round a box anchored to the world, so the particles don't swim as the box follows the focus. Rain (Kind 0) is a streak along
// its fall, lit by all the light at its place, the light the fog gets, and left out inside indoor volumes; dust (Kind 1) is a
// soft mote wobbling on the wind, lit only by the light that reaches it straight from the sun or a lamp, so it shows where light
// falls through it and vanishes in shade.

#include "Shading.hlsl"

struct WeatherConstants
{
    uint Frame;
    uint Kind;
    uint Count;
    float Time;
    // The box particles fill: its centre, and its size.
    float3 Center;
    float Brightness;
    float3 Size;
    // A rain streak's length in world units.
    float Length;
    float3 Velocity;
    // A particle's width in render pixels.
    float Width;
};

[[vk::push_constant]] ConstantBuffer<WeatherConstants> Pass : register(b0, space0);

static const uint KindRain = 0;

struct Particle
{
    float4 Position : SV_Position;
    // Across the particle from -1 to 1, then along it from its head (1) to its tail (0).
    float2 Place : TEXCOORD0;
    nointerpolation float3 Color : COLOR0;
};

float3 Hash3(uint number)
{
    uint3 value = uint3(number, number * 16807u + 1u, number * 48271u + 7u);
    value = value * 1664525u + 1013904223u;
    value.x += value.y * value.z;
    value.y += value.z * value.x;
    value.z += value.x * value.y;
    value ^= value >> 16u;
    value.x += value.y * value.z;
    value.y += value.z * value.x;
    value.z += value.x * value.y;
    return float3(value & 0xFFFFFF) / 16777216.0;
}

// Where the particle stands now: its own place in the box moved along its velocity, wrapped round the box.
float3 PlaceOf(float3 seed, float3 velocity, float time)
{
    float3 boxMin = Pass.Center - Pass.Size * 0.5;
    float3 moved = seed * Pass.Size + velocity * time - boxMin;
    return boxMin + (moved - floor(moved / Pass.Size) * Pass.Size);
}

float2 ToPixel(FrameData frame, float4 clip)
{
    float2 ndc = clip.xy / clip.w;
    return float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5) * frame.TargetSize;
}

Particle VertexMain(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    float3 seed = Hash3(instanceId);
    float speed = 0.8 + 0.4 * seed.y;
    float3 velocity = Pass.Velocity * speed;
    float3 head = PlaceOf(seed, velocity, Pass.Time);
    float3 tail = head;
    if (Pass.Kind == KindRain)
    {
        tail = head - normalize(velocity) * Pass.Length;
    }
    else
    {
        float phase = seed.z * 6.2831853;
        float3 wobble = float3(sin(Pass.Time * 0.7 + phase), sin(Pass.Time * 0.5 + phase * 1.3), cos(Pass.Time * 0.6 + phase)) * 0.15;
        head += wobble;
        tail = head;
    }

    // A particle left out collapses to one point before the near plane, so nothing is drawn.
    Particle output = (Particle)0;
    output.Position = float4(0, 0, -1, 1);
    if (Pass.Kind == KindRain && IsIndoors(frame, head))
    {
        return output;
    }

    float4 headClip = mul(float4(head, 1), frame.ViewProjection);
    float4 tailClip = mul(float4(tail, 1), frame.ViewProjection);
    float2 headPixel = ToPixel(frame, headClip);
    float2 tailPixel = ToPixel(frame, tailClip);
    float2 along = headPixel - tailPixel;
    if (dot(along, along) < 1e-4)
    {
        along = float2(0, -1);
    }

    along = normalize(along);
    float2 across = float2(-along.y, along.x);
    bool atHead = vertexId < 2;
    float side = (vertexId & 1) * 2.0 - 1;
    float2 pixel = atHead ? headPixel : tailPixel;
    float4 clip = atHead ? headClip : tailClip;
    float2 offset = across * side * Pass.Width * 0.5;
    if (Pass.Kind != KindRain)
    {
        offset += along * (atHead ? 1 : -1) * Pass.Width * 0.5;
    }

    float2 moved = pixel + offset;
    float2 ndc = float2(moved.x / frame.TargetSize.x * 2 - 1, 1 - moved.y / frame.TargetSize.y * 2);
    output.Position = float4(ndc * clip.w, clip.z, clip.w);
    output.Place = float2(side, atHead ? 1 : 0);
    float3 toCamera = ToCamera(frame, head);
    float3 light = DirectFogLight(frame, headPixel, head, toCamera);
    if (Pass.Kind == KindRain)
    {
        light += frame.Ambient * IndoorAmbient(frame, head) + SampleProbes(frame, head, 0, toCamera);
    }

    output.Color = light * Pass.Brightness;
    return output;
}

float4 PixelMain(Particle particle) : SV_Target
{
    float fade;
    if (Pass.Kind == KindRain)
    {
        fade = particle.Place.y * (1 - abs(particle.Place.x));
    }
    else
    {
        float2 disc = float2(particle.Place.x, particle.Place.y * 2 - 1);
        fade = saturate(1 - dot(disc, disc));
    }

    return float4(particle.Color * fade, 0);
}
