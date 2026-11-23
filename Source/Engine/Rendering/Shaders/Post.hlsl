// Turns the lit scene into the final image: adds bloom, then exposure, contrast, saturation and tint, then the tonemapper's
// curve, then the camera's depth fade (nearer than the focus darkens, farther melts into a colour), then lays it over the canvas. The scene's alpha is how much paint lies there (0 to 1) plus how thick it
// stands (0 to 1, from a painting's impasto): bare canvas shows where there is none, and the canvas's weave shows through
// paint that isn't thick, so everything looks painted on it; only thick impasto hides it. The
// canvas is flat and moves with the world as an orthographic camera pans and zooms.
// Draws one triangle that covers the target.

#include "Frame.hlsl"

struct PostConstants
{
    float3 Tint;
    float Exposure;
    float Contrast;
    float Saturation;
    float Bloom;
    float CanvasIntensity;
    uint Scene;
    uint BloomTexture;
    uint Canvas;
    uint ClampSampler;
    uint WrapSampler;
    uint Tonemapper;
    float WhitePoint;
    float CanvasImageHeight;
    float3 CanvasTint;
    float CanvasPanRight;
    float CanvasPanUp;
    uint Frame;
    uint Depth;
};

[[vk::push_constant]] ConstantBuffer<PostConstants> Post : register(b0, space0);

// The bloom levels the upsampling summed, which averaging divides back out.
static const float BloomLevels = 5;

// Where the pixel lies on the flat canvas, in image uvs: the image CanvasImageHeight pixels high, slid by the camera's pan so
// the canvas moves with the world.
float2 CanvasUv(float2 pixel, float2 canvasSize)
{
    float2 imagePixels = float2(Post.CanvasImageHeight * canvasSize.x / canvasSize.y, Post.CanvasImageHeight);
    float2 panned = pixel + float2(Post.CanvasPanRight, -Post.CanvasPanUp);
    return panned / imagePixels;
}

// Linear middle grey, which contrast pivots on.
static const float MiddleGrey = 0.18;

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(corner * 2 - 1, 0, 1);
}

// The Tonemapper enum's values.
static const uint TonemapUncharted2 = 1;
static const uint TonemapAces = 2;

// John Hable's filmic curve from Uncharted 2, with his shoulder, linear and toe constants.
float3 Hable(float3 x)
{
    const float A = 0.15;
    const float B = 0.50;
    const float C = 0.10;
    const float D = 0.20;
    const float E = 0.02;
    const float F = 0.30;
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

// Krzysztof Narkowicz's fit of the ACES film curve.
float3 Aces(float3 x)
{
    return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
}

// Brings the graded light into the screen's range by the mood's tonemapper; Hable's exposure bias of 2 keeps middle grey
// where the untonemapped image has it.
float3 Tonemap(float3 color)
{
    switch (Post.Tonemapper)
    {
        case TonemapUncharted2:
            return Hable(color * 2) / Hable(Post.WhitePoint);
        case TonemapAces:
            return Aces(color);
        default:
            return color;
    }
}

// 0 at or before start, 1 at or past end, eased between.
float Ramp(float start, float end, float value)
{
    float amount = saturate((value - start) / max(end - start, 1e-4));
    return amount * amount * (3 - 2 * amount);
}

// Darkens what stands nearer than the frame's focus and melts what lies farther into the far colour, by their depth along the
// view from the focus; only where there is paint.
float3 FadeByDepth(float2 pixel, float3 color, float coverage)
{
    FrameData frame = LoadFrame(Post.Frame);
    if (frame.NearAmount <= 0 && frame.FarAmount <= 0)
    {
        return color;
    }

    float depth = Textures[Post.Depth].Load(int3(pixel, 0)).r;
    float3 world = WorldAtPixel(frame, pixel, depth);
    float beyondFocus = dot(float4(world, 1), frame.DepthAxis) - frame.FocusDepth;
    float near = frame.NearAmount * Ramp(frame.NearStart, frame.NearEnd, -beyondFocus) * coverage;
    float far = frame.FarAmount * Ramp(frame.FarStart, frame.FarEnd, beyondFocus) * coverage;
    color *= 1 - near;
    return lerp(color, frame.FarColor, far);
}

float4 PixelMain(float4 position : SV_Position) : SV_Target
{
    Texture2D scene = Textures[Post.Scene];
    float4 sceneColor = scene.Load(int3(position.xy, 0));
    float3 color = sceneColor.rgb;
    if (Post.BloomTexture != None)
    {
        uint width;
        uint height;
        scene.GetDimensions(width, height);
        float2 uv = position.xy / float2(width, height);
        float3 bloom = Textures[Post.BloomTexture].SampleLevel(Samplers[Post.ClampSampler], uv, 0).rgb / BloomLevels;
        color = lerp(color, bloom, Post.Bloom);
    }

    color *= Post.Exposure;
    color = pow(max(color, 0) / MiddleGrey, Post.Contrast) * MiddleGrey;
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    color = lerp(luminance, color, Post.Saturation);
    color *= Post.Tint;
    color = Tonemap(color);

    float coverage = saturate(sceneColor.a);
    color = FadeByDepth(position.xy, color, coverage);
    if (Post.Canvas != None)
    {
        Texture2D canvas = Textures[Post.Canvas];
        uint width;
        uint height;
        uint levels;
        canvas.GetDimensions(0, width, height, levels);
        float2 uv = CanvasUv(position.xy, float2(width, height));
        // Sampled through its mips, so a small window shows the weave softened rather than broken into aliased specks.
        float3 sampled = canvas.Sample(Samplers[Post.WrapSampler], uv).rgb * Post.CanvasTint;
        float3 averageCanvas = canvas.SampleLevel(Samplers[Post.WrapSampler], uv, levels - 1).rgb * Post.CanvasTint;

        // Intensity scales how far the canvas strays from its average colour: its texture fades, or grows stronger.
        float3 canvasColor = max(lerp(averageCanvas, sampled, Post.CanvasIntensity), 0);
        float3 toLuminance = float3(0.2126, 0.7152, 0.0722);
        float averageLuminance = max(dot(averageCanvas, toLuminance), 1e-3);

        // Paint that isn't thick lets the canvas's weave through: the canvas over its own average brightness, so it keeps the
        // painted colour's light and takes the canvas's texture and hue.
        float thickness = saturate(sceneColor.a - 1);
        float3 weave = canvasColor / averageLuminance;
        float3 painted = lerp(color * weave, color, thickness);

        color = lerp(canvasColor, painted, coverage);
        coverage = 1;
    }

    // Coverage: 0 where nothing was drawn and there is no canvas, so a stage's image shows over the interface; light added
    // there (bloom) stays additive.
    return float4(color, coverage);
}
