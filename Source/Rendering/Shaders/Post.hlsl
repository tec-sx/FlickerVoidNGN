// Turns the lit scene into the final image: adds bloom, then exposure, contrast, saturation and tint, then grain and a
// paper texture laid over the screen. Draws one triangle that covers the target.

Texture2D Textures[] : register(t0, space1);
SamplerState Samplers[] : register(s0, space3);

struct PostConstants
{
    float3 Tint;
    float Exposure;
    float Contrast;
    float Saturation;
    float Bloom;
    float Grain;
    uint Scene;
    uint BloomTexture;
    uint Paper;
    uint ClampSampler;
    uint WrapSampler;
};

[[vk::push_constant]] ConstantBuffer<PostConstants> Post : register(b0, space0);

static const uint None = 0xFFFFFFFF;

// The bloom levels the upsampling summed, which averaging divides back out.
static const float BloomLevels = 5;

// Linear middle grey, which contrast pivots on.
static const float MiddleGrey = 0.18;

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(corner * 2 - 1, 0, 1);
}

float PixelNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

float4 PixelMain(float4 position : SV_Position) : SV_Target
{
    Texture2D scene = Textures[Post.Scene];
    float3 color = scene.Load(int3(position.xy, 0)).rgb;
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

    if (Post.Paper != None)
    {
        Texture2D paper = Textures[Post.Paper];
        uint width;
        uint height;
        paper.GetDimensions(width, height);
        float2 uv = position.xy / float2(width, height);
        color *= paper.SampleLevel(Samplers[Post.WrapSampler], uv, 0).rgb;
    }

    color += (PixelNoise(position.xy) - 0.5) * Post.Grain;
    return float4(color, 1);
}
