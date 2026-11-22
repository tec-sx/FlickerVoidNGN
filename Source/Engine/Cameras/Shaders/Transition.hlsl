// Lays a camera transition over the frame: the previous shot's live view, rendered into a texture of the same size, dissolving
// away, then the fade colour. One triangle covers the target; the result is premultiplied, blended over what is there.

Texture2D Textures[] : register(t0, space1);

struct TransitionConstants
{
    uint DissolveTexture;
    float Dissolve;
    float Fade;
    uint Padding;
    float4 FadeColor;
};

[[vk::push_constant]] ConstantBuffer<TransitionConstants> Transition : register(b0, space0);

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    return float4((corner * 2) - 1, 0, 1);
}

float4 PixelMain(float4 position : SV_Position) : SV_Target
{
    float3 color = 0;
    if (Transition.Dissolve > 0)
    {
        float4 previous = Textures[Transition.DissolveTexture].Load(int3(position.xy, 0));
        color = previous.rgb * Transition.Dissolve;
    }

    float keep = 1 - Transition.Fade;
    color = (color * keep) + (Transition.FadeColor.rgb * Transition.Fade);
    float cover = 1 - ((1 - Transition.Dissolve) * keep);
    return float4(color, cover);
}
