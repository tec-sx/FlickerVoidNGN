// Draws a line around the silhouettes of the outline mask, in the color of the nearest one, over the output. Draws one
// triangle that covers the target; pixels inside a silhouette or further than the width from one are left as they are.

Texture2D Textures[] : register(t0, space1);

struct OutlineConstants
{
    uint Mask;
    uint Width;
};

[[vk::push_constant]] ConstantBuffer<OutlineConstants> Outline : register(b0, space0);

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(corner * 2 - 1, 0, 1);
}

float4 PixelMain(float4 position : SV_Position) : SV_Target
{
    Texture2D mask = Textures[Outline.Mask];
    int2 pixel = int2(position.xy);
    if (mask.Load(int3(pixel, 0)).a > 0)
    {
        discard;
    }

    uint maskWidth;
    uint maskHeight;
    mask.GetDimensions(maskWidth, maskHeight);
    int2 last = int2(maskWidth, maskHeight) - 1;
    int width = (int)Outline.Width;
    int reach = width * width;
    int nearest = reach + 1;
    float3 color = 0;
    for (int y = -width; y <= width; y++)
    {
        for (int x = -width; x <= width; x++)
        {
            int distance = x * x + y * y;
            if (distance >= nearest)
            {
                continue;
            }

            int2 neighbour = clamp(pixel + int2(x, y), 0, last);
            float4 texel = mask.Load(int3(neighbour, 0));
            if (texel.a > 0)
            {
                nearest = distance;
                color = texel.rgb;
            }
        }
    }

    if (nearest > reach)
    {
        discard;
    }

    return float4(color, 1);
}
