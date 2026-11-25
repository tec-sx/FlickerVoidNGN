// Shows one of the G-buffer's targets or the depth in place of the lit frame (DebugView.cs), over the whole output whatever the
// render size, black where nothing was drawn: the albedo; the world normal as half of each axis plus half; each material in a
// color of its own, lightened where the paint stands thick; the object depth and each pixel's own view depth as grey, depth
// over depth plus 10; and the motion as middle grey when still, red and green across and down, full at 8 pixels.

#include "Frame.hlsl"
#include "GBuffer.hlsl"

struct DebugViewConstants
{
    uint Frame;
    uint View;
    // Render pixels to an output pixel, across and down: the G-buffer is drawn at the render size.
    float2 Scale;
};

[[vk::push_constant]] ConstantBuffer<DebugViewConstants> Pass : register(b0, space0);

// DebugView's values.
static const uint ViewAlbedo = 1;
static const uint ViewNormal = 2;
static const uint ViewMaterial = 3;
static const uint ViewObjectDepth = 4;
static const uint ViewMotion = 5;
static const uint ViewDepth = 6;

// The view depth at which the depth views show middle grey.
static const float DepthHalf = 10;

// The motion, in pixels, that the motion view shows at full red or green.
static const float MotionFull = 8;

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(corner * 2 - 1, 0, 1);
}

float DepthShade(float depth)
{
    float near = max(depth, 0);
    return near / (near + DepthHalf);
}

// A color of the material's own, from its bindless index, never too dark to see.
float3 MaterialColor(uint material)
{
    uint hash = material * 2654435761u;
    float3 color = float3(hash & 0xFF, (hash >> 8) & 0xFF, (hash >> 16) & 0xFF) / 255;
    return 0.25 + 0.75 * color;
}

float3 Show(FrameData frame, uint view, int3 texel, float2 pixel, float depth)
{
    switch (view)
    {
        case ViewAlbedo:
            return Textures[frame.GBufferAlbedo].Load(texel).rgb;
        case ViewNormal:
        {
            float2 folded = Textures[frame.GBufferNormal].Load(texel).xy;
            float3 normal = DecodeNormal(folded);
            return normal * 0.5 + 0.5;
        }
        case ViewMaterial:
        {
            float4 bytes = Textures[frame.GBufferMaterial].Load(texel);
            SurfaceMaterial surface = DecodeMaterial(bytes);
            float3 color = MaterialColor(surface.Material);
            return lerp(color, 1, 0.5 * surface.Thickness);
        }
        case ViewObjectDepth:
        {
            float objectDepth = Textures[frame.GBufferObjectDepth].Load(texel).r;
            return DepthShade(objectDepth);
        }
        case ViewMotion:
        {
            float2 motion = Textures[frame.GBufferMotion].Load(texel).xy;
            float2 shown = 0.5 + 0.5 * clamp(motion / MotionFull, -1, 1);
            return float3(shown, 0.5);
        }
        default:
        {
            float3 world = WorldAtPixel(frame, pixel, depth);
            float viewDepth = dot(float4(world, 1), frame.DepthAxis);
            return DepthShade(viewDepth);
        }
    }
}

float4 PixelMain(float4 position : SV_Position) : SV_Target
{
    FrameData frame = LoadFrame(Pass.Frame);
    int3 texel = int3(position.xy * Pass.Scale, 0);
    float depth = Textures[frame.Depth].Load(texel).r;
    if (depth >= 1)
    {
        return float4(0, 0, 0, 1);
    }

    float2 pixel = float2(texel.xy) + 0.5;
    float3 color = Show(frame, Pass.View, texel, pixel, depth);
    return float4(color, 1);
}
