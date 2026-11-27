// The geometry pass's pixel shader, included after SceneSurface.hlsl and a SurfaceAt function that reads a draw's surface: the
// scene shader's reads it as every material without a master does, a master's through its SurfaceOf.

#pragma once

// The G-buffer's targets, in GBuffer.Formats' order.
struct GeometryOutput
{
    float4 Albedo : SV_Target0;
    float2 Normal : SV_Target1;
    float4 Material : SV_Target2;
    float ObjectDepth : SV_Target3;
    float2 Motion : SV_Target4;
};

// The geometry pass leaves the surface for lighting, unlit, but where the cutaway cuts it away or a cutout card's paint is
// thinner than its material's cutoff. The object depth leans from the pixel's own view depth to its box centre's as the draw is
// small (Draw.Wholeness; the centre is the draw's or its copy's), and the motion is where the surface stood in the frame before less where it stands now, in pixels.
GeometryOutput GeometryMain(VertexOutput input, bool front : SV_IsFrontFace)
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    if (IsCut(frame, draw, input.WorldPosition))
    {
        discard;
    }

    Material material = LoadMaterial(draw.Material);
    Surface surface = SurfaceAt(input, draw, material);
    if (surface.Albedo.a < material.Cutoff)
    {
        discard;
    }

    // A two-sided master's back faces turn their normal toward the camera; the rest are culled.
    if (!front)
    {
        surface.Normal = -surface.Normal;
    }

    GeometryOutput output;
    output.Albedo = surface.Albedo;
    output.Normal = EncodeNormal(surface.Normal);
    output.Material = EncodeMaterial(draw.Material, surface.Impasto, draw.Flags);
    float depth = dot(float4(input.WorldPosition, 1), frame.DepthAxis);
    float centerDepth = dot(float4(input.Center, 1), frame.DepthAxis);
    output.ObjectDepth = lerp(depth, centerDepth, draw.Wholeness);
    // The pixel without the jitter, as the frame before's view is without it too.
    float2 previous = ClipToPixel(frame, input.PreviousClip);
    output.Motion = previous - (input.Position.xy - frame.Jitter);
    return output;
}
