// Turns the lit scene into the final image: mixes in bloom (more where the lens's dirt is bright), adds the lens flare, then grades it (Grade.hlsl: exposure, contrast, saturation and tint,
// then the tonemapper's curve), then lays it over the canvas. The scene's alpha is how much paint lies there (0 to 1) plus how
// thick it stands (0 to 1, from a painting's impasto): bare canvas shows only where nothing was painted, and under paint the
// canvas never changes its colour, it shows as its threads, the more the thinner the paint, so shadows stay shadows. The canvas is flat and moves
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
    float VignetteIntensity;
    float VignetteRadius;
    float VignetteSoftness;
    float VignetteRoundness;
    float DirtAmount;
    uint Dirt;
    uint Flare;
    float3 VignetteColor;
};

[[vk::push_constant]] ConstantBuffer<PostConstants> Post : register(b0, space0);

// Where the pixel lies on the flat canvas, in image uvs: the image CanvasImageHeight pixels high, slid by the camera's pan so
// the canvas moves with the world.
float2 CanvasUv(float2 pixel, float2 canvasSize)
{
    float2 imagePixels = float2(Post.CanvasImageHeight * canvasSize.x / canvasSize.y, Post.CanvasImageHeight);
    float2 panned = pixel + float2(Post.CanvasPanRight, -Post.CanvasPanUp);
    return panned / imagePixels;
}

// How much the vignette takes this pixel, from 0 to the intensity: nothing within the radius, then rising over the softness. The
// distance is measured to the corner, with roundness 1 making it a circle and 0 following the screen's shape.
float Vignette(float2 uv, float aspect)
{
    float2 centered = uv * 2 - 1;
    centered.x *= lerp(1, aspect, Post.VignetteRoundness);
    float reach = length(float2(lerp(1, aspect, Post.VignetteRoundness), 1));
    float distanceFromMiddle = length(centered) / reach;
    return Post.VignetteIntensity * smoothstep(Post.VignetteRadius, Post.VignetteRadius + Post.VignetteSoftness, distanceFromMiddle);
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
    uint width;
    uint height;
    scene.GetDimensions(width, height);
    float2 uv = position.xy / float2(width, height);
    float dirt = 0;
    if (Post.Dirt != None)
    {
        dirt = Textures[Post.Dirt].SampleLevel(Samplers[Post.ClampSampler], uv, 0).r * Post.DirtAmount;
    }

    if (Post.BloomTexture != None)
    {
        float3 bloom = Textures[Post.BloomTexture].SampleLevel(Samplers[Post.ClampSampler], uv, 0).rgb;
        color = lerp(color, bloom, saturate(Post.Bloom + dirt));
    }

    // The lens flare adds its light, raised where the lens's dirt catches it, so it shows with no bloom at all.
    if (Post.Flare != None)
    {
        float3 flare = Textures[Post.Flare].SampleLevel(Samplers[Post.ClampSampler], uv, 0).rgb;
        color += flare * (1 + dirt);
    }

    color = Grade(Post.Grade, color);
    if (sceneColor.a > 0 && Post.VignetteIntensity > 0)
    {
        color = lerp(color, Post.VignetteColor, Vignette(uv, (float)width / height));
    }

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

        // The canvas never colours the paint: it shows as its threads alone, its brightness over its own average, the more the
        // thinner the paint is, and not at all under opaque paint or thick impasto. Only where nothing was painted is it bare.
        if (sceneColor.a <= 0)
        {
            color = canvasColor;
        }
        else
        {
            float threads = dot(canvasColor, ToLuminance) / averageLuminance;
            float thickness = saturate(sceneColor.a - 1);
            float thin = (1 - coverage * coverage) * (1 - thickness);
            color *= lerp(1, threads, thin);
        }

        coverage = 1;
    }

    // Coverage: 0 where nothing was drawn and there is no canvas, so a stage's image shows over the interface, and light added
    // there (bloom) stays additive; whatever was painted is whole, however thin, as thin paint shows its canvas and not what
    // lies behind the image.
    if (sceneColor.a > 0)
    {
        coverage = 1;
    }

    return float4(color, coverage);
}
