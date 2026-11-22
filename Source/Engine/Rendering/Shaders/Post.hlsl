// Turns the lit scene into the final image: adds bloom, then exposure, contrast, saturation and tint, then the tonemapper's
// curve, then lays it over the paper, then grain. The scene's alpha is how much paint lies there (0 to 1) plus how thick it
// stands (0 to 1, from a painting's impasto): bare paper shows where there is none, and the canvas's weave shows through
// paint that isn't thick, so everything looks painted on it; only thick impasto hides it. The
// paper lies on a sphere around the camera like a sky, so it turns as the camera turns, and slides with an orthographic pan.
// Draws one triangle that covers the target.

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
    uint Tonemapper;
    float WhitePoint;
    float PaperGrain;
    float3 PaperRight;
    float PaperPanRight;
    float3 PaperUp;
    float PaperPanUp;
    float3 PaperForward;
    float PaperPadding;
};

[[vk::push_constant]] ConstantBuffer<PostConstants> Post : register(b0, space0);

static const uint None = 0xFFFFFFFF;

// The bloom levels the upsampling summed, which averaging divides back out.
static const float BloomLevels = 5;

// The field of view the paper's sphere is seen with, whatever the camera's projection: an orthographic camera's rays are all
// parallel, so the sphere is looked at as through a lens of this width.
static const float PaperFieldOfView = 1.0471976;

static const float Pi = 3.14159265;

// Where the pixel's ray meets the paper's sphere, in texels-sized uvs: longitude across and latitude down, the paper repeated
// a whole number of times around so it meets itself, at about one texel a pixel, then slid by the camera's pan.
float2 PaperUv(float2 pixel, float2 size, float2 paperSize)
{
    float2 clip = pixel / size * 2 - 1;
    float halfHeight = tan(PaperFieldOfView * 0.5);
    float halfWidth = halfHeight * size.x / size.y;
    float3 ray = normalize(Post.PaperForward + Post.PaperRight * (clip.x * halfWidth) - Post.PaperUp * (clip.y * halfHeight));
    float longitude = atan2(ray.x, -ray.z) / (2 * Pi) + 0.5;
    float latitude = acos(clamp(ray.y, -1, 1)) / Pi;
    float texelsAround = size.y * 2 * Pi / PaperFieldOfView;
    float repeats = max(round(texelsAround / paperSize.x), 1);
    float2 uv = float2(longitude * repeats, latitude * repeats * 0.5 * paperSize.x / paperSize.y);
    return uv + float2(Post.PaperPanRight, -Post.PaperPanUp) / paperSize;
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

float PixelNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
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
    if (Post.Paper != None)
    {
        Texture2D paper = Textures[Post.Paper];
        uint width;
        uint height;
        uint levels;
        paper.GetDimensions(0, width, height, levels);
        uint targetWidth;
        uint targetHeight;
        scene.GetDimensions(targetWidth, targetHeight);
        float2 uv = PaperUv(position.xy, float2(targetWidth, targetHeight), float2(width, height));
        float3 paperColor = paper.SampleLevel(Samplers[Post.WrapSampler], uv, 0).rgb;
        float3 averagePaper = paper.SampleLevel(Samplers[Post.WrapSampler], uv, levels - 1).rgb;
        float3 toLuminance = float3(0.2126, 0.7152, 0.0722);
        float averageLuminance = max(dot(averagePaper, toLuminance), 1e-3);

        // Paint that isn't thick lets the canvas's weave through: the paper over its own average brightness, so it keeps the
        // painted colour's light and takes the canvas's texture and hue.
        float thickness = saturate(sceneColor.a - 1);
        float3 weave = paperColor / averageLuminance;
        float3 painted = lerp(color * weave, color, thickness);

        // Thin paint fills the paper's valleys (darker than the paper's average) first and leaves its peaks bare; bare paper
        // and fully painted paper stay as they are.
        float grain = dot(averagePaper - paperColor, toLuminance) / averageLuminance;
        float paint = coverage;
        float covered = saturate(paint + grain * Post.PaperGrain * 4 * paint * (1 - paint));
        color = lerp(paperColor, painted, covered);
        coverage = 1;
    }

    color += (PixelNoise(position.xy) - 0.5) * Post.Grain;

    // Coverage: 0 where nothing was drawn and there is no paper, so a stage's image shows over the interface; light added
    // there (bloom) stays additive.
    return float4(color, coverage);
}
