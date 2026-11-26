// Turns the lit scene into the final image: adds bloom, then grades it (Grade.hlsl: exposure, contrast, saturation and tint,
// then the tonemapper's curve), then lays it over the canvas. The scene's alpha is how much paint lies there (0 to 1) plus how
// thick it stands (0 to 1, from a painting's impasto): bare canvas shows where there is none, and the canvas's weave shows
// through paint that isn't thick, so everything looks painted on it; only thick impasto hides it. The canvas under paint takes
// the light and shadow of the surface, as much as the mood's canvas shading says; bare canvas keeps its own colour. The canvas is flat and moves
// with the world as an orthographic camera pans and zooms. The camera's depth fade was laid on the lit scene before
// (DepthFade.hlsl), as was the sky (Sky.hlsl). Draws one triangle that covers the target.

#include "Frame.hlsl"
#include "Grade.hlsl"

struct PostConstants
{
    Grading Grade;
    float Bloom;
    float CanvasIntensity;
    uint Scene;
    uint BloomTexture;
    uint Canvas;
    uint ClampSampler;
    uint WrapSampler;
    float CanvasImageHeight;
    float3 CanvasTint;
    float CanvasPanRight;
    float CanvasPanUp;
    uint CanvasLight;
    float CanvasShading;
};

[[vk::push_constant]] ConstantBuffer<PostConstants> Post : register(b0, space0);

// The bloom levels the upsampling summed, which averaging divides back out.
static const float BloomLevels = 5;

// How the canvas under thin paint is lit at the pixel, by CanvasShading: the light its surface takes, averaged over the 4 by 4
// pixels round it (traced shadows are noisy while an upscaler denoises the scene, and only the scene is denoised), through the
// grade, as a share of what full light grades to, so the canvas darkens in shadow as much as the paint looks to.
float3 CanvasShade(float2 pixel, float2 outputSize)
{
    if (Post.CanvasShading <= 0)
    {
        return 1;
    }

    Texture2D lights = Textures[Post.CanvasLight];
    uint width;
    uint height;
    lights.GetDimensions(width, height);
    float2 uv = pixel / outputSize;
    float2 texel = 1 / float2(width, height);
    SamplerState clamp = Samplers[Post.ClampSampler];
    float3 light = lights.SampleLevel(clamp, uv + texel * float2(-1, -1), 0).rgb;
    light += lights.SampleLevel(clamp, uv + texel * float2(1, -1), 0).rgb;
    light += lights.SampleLevel(clamp, uv + texel * float2(-1, 1), 0).rgb;
    light += lights.SampleLevel(clamp, uv + texel * float2(1, 1), 0).rgb;
    light *= 0.25;
    float3 lit = Grade(Post.Grade, MiddleGrey * light);
    float3 full = Grade(Post.Grade, float3(MiddleGrey, MiddleGrey, MiddleGrey));
    float3 shade = lit / max(full, 1e-4);
    return lerp(1, shade, Post.CanvasShading);
}

// Where the pixel lies on the flat canvas, in image uvs: the image CanvasImageHeight pixels high, slid by the camera's pan so
// the canvas moves with the world.
float2 CanvasUv(float2 pixel, float2 canvasSize)
{
    float2 imagePixels = float2(Post.CanvasImageHeight * canvasSize.x / canvasSize.y, Post.CanvasImageHeight);
    float2 panned = pixel + float2(Post.CanvasPanRight, -Post.CanvasPanUp);
    return panned / imagePixels;
}

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(corner * 2 - 1, 0, 1);
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

    color = Grade(Post.Grade, color);
    float coverage = saturate(sceneColor.a);
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
        float averageLuminance = max(dot(averageCanvas, ToLuminance), 1e-3);

        // Paint that isn't thick lets the canvas's weave through: the canvas over its own average brightness, so it keeps the
        // painted colour's light and takes the canvas's texture and hue.
        float thickness = saturate(sceneColor.a - 1);
        float3 weave = canvasColor / averageLuminance;
        float3 painted = lerp(color * weave, color, thickness);

        uint sceneWidth;
        uint sceneHeight;
        scene.GetDimensions(sceneWidth, sceneHeight);
        float3 shade = CanvasShade(position.xy, float2(sceneWidth, sceneHeight));
        color = lerp(canvasColor * shade, painted, coverage);
        coverage = 1;
    }

    // Coverage: 0 where nothing was drawn and there is no canvas, so a stage's image shows over the interface; light added
    // there (bloom) stays additive.
    return float4(color, coverage);
}
