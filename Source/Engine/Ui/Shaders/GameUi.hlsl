// Draws the game's interface over the frame: one triangle covers the target and each pixel reads the interface's pixel at the same
// place (stored BGRA, read as RGBA), with premultiplied alpha in sRGB, made linear when the target stores linear values.

Texture2D Textures[] : register(t0, space1);

struct GameUiConstants
{
    uint Texture;
    uint LinearOutput;
};

[[vk::push_constant]] ConstantBuffer<GameUiConstants> Ui : register(b0, space0);

float3 SrgbToLinear(float3 color)
{
    float3 low = color / 12.92;
    float3 high = pow((color + 0.055) / 1.055, 2.4);
    return lerp(low, high, step(0.04045, color));
}

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    return float4((uv * 2) - 1, 0, 1);
}

float4 PixelMain(float4 position : SV_Position) : SV_Target
{
    float4 color = Textures[Ui.Texture].Load(int3(position.xy, 0));
    if (Ui.LinearOutput != 0 && color.a > 0)
    {
        float3 straight = color.rgb / color.a;
        color.rgb = SrgbToLinear(straight) * color.a;
    }

    return color;
}
