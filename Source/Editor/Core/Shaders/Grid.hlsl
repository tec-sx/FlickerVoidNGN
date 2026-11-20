// The editor's ground grid on the plane y = 0: one triangle covers the screen, each pixel finds where its view ray meets the
// plane and writes that point's depth, so the scene hides the grid. Lines every cell and every major cell, the X axis red and the
// Z axis blue, all thin and faint so they only hint at the ground; lines fade where cells shrink to a few pixels and toward
// the horizon.

struct GridConstants
{
    // The view's matrices multiply row vectors, as System.Numerics' do; only the columns that give depth are kept of the forward one.
    row_major float4x4 InverseViewProjection;
    float4 DepthColumn;
    float4 WColumn;
    float CellSize;
    float MajorCellSize;
    float Opacity;
    float Padding;
};

[[vk::push_constant]] ConstantBuffer<GridConstants> Grid : register(b0, space0);

static const float3 LineColor = float3(0.62, 0.6, 0.52);
static const float3 XAxisColor = float3(0.9, 0.15, 0.15);
static const float3 ZAxisColor = float3(0.15, 0.35, 1.0);
static const float MinorAlpha = 0.12;
static const float MajorAlpha = 0.22;
static const float AxisWidth = 0.75;
static const float AxisAlpha = 0.7;
// Lines are this many pixels wide, so the grid only hints at the ground.
static const float LineWidth = 0.5;

struct PixelOutput
{
    float4 Color : SV_Target;
    float Depth : SV_Depth;
};

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 Clip : TEXCOORD0;
};

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    VertexOutput output;
    output.Clip = corner * 2 - 1;
    output.Position = float4(output.Clip, 0, 1);
    return output;
}

// The world position on the view ray through a point on the screen, at a depth from 0 (near) to 1 (far).
float3 WorldAt(float2 clip, float depth)
{
    float4 world = mul(float4(clip, depth, 1), Grid.InverseViewProjection);
    return world.xyz / world.w;
}

// How strongly a pixel lies on a line of a grid with the given spacing: 1 on a line a pixel wide, fading as cells get too small to tell apart.
float GridLine(float2 coordinate, float spacing)
{
    float2 cell = coordinate / spacing;
    float2 width = fwidth(cell);
    float2 distance = abs(frac(cell - 0.5) - 0.5) / (width * LineWidth);
    float onLine = 1 - min(min(distance.x, distance.y), 1);
    float density = max(width.x, width.y);
    return onLine * (1 - smoothstep(0.1, 0.3, density));
}

// How strongly a pixel lies on the line where a coordinate is zero.
float AxisLine(float coordinate)
{
    return 1 - min(abs(coordinate) / (fwidth(coordinate) * AxisWidth), 1);
}

PixelOutput PixelMain(VertexOutput input)
{
    float3 nearPoint = WorldAt(input.Clip, 0);
    float3 farPoint = WorldAt(input.Clip, 1);
    float3 ray = farPoint - nearPoint;
    float along = -nearPoint.y / ray.y;
    float3 hit = nearPoint + ray * along;
    float2 ground = hit.xz;

    float minor = GridLine(ground, Grid.CellSize) * MinorAlpha;
    float major = GridLine(ground, Grid.MajorCellSize) * MajorAlpha;
    float alpha = max(minor, major);
    float3 color = LineColor;
    float xAxis = AxisLine(hit.z);
    float zAxis = AxisLine(hit.x);
    if (xAxis > 0)
    {
        color = lerp(color, XAxisColor, xAxis);
        alpha = max(alpha, xAxis * AxisAlpha);
    }

    if (zAxis > 0)
    {
        color = lerp(color, ZAxisColor, zAxis);
        alpha = max(alpha, zAxis * AxisAlpha);
    }

    // Grazing rays crowd the lines together, so they fade toward the horizon.
    float3 direction = normalize(ray);
    alpha *= saturate(abs(direction.y) * 4);
    alpha *= Grid.Opacity;

    bool hitsPlane = along >= 0 && along <= 1;
    if (!hitsPlane)
    {
        alpha = 0;
    }

    clip(alpha - 0.001);
    float4 hitPosition = float4(hit, 1);
    float depth = dot(hitPosition, Grid.DepthColumn) / dot(hitPosition, Grid.WColumn);
    PixelOutput output;
    output.Color = float4(color, alpha);
    output.Depth = saturate(depth);
    return output;
}
