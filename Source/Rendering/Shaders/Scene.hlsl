// Draws a mesh part whose vertices (float3 position, float3 normal, float2 uv) are pulled from a bindless buffer: its base
// color times its texture, lit by the sun, an even ambient light, and the point and spot lights of the pixel's cluster.
// The frame buffer holds FrameData, then the lights, then each cluster's range of light indices, then the indices.

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);

struct SceneConstants
{
    row_major float4x4 World;
    float4 BaseColor;
    uint Frame;
    uint Vertices;
    uint Texture;
    uint Sampler;
};

struct FrameData
{
    float4x4 ViewProjection;
    float4 DepthAxis;
    float3 SunDirection;
    float Ambient;
    float2 TargetSize;
    float SliceScale;
    float SliceBias;
    uint LogarithmicSlices;
    uint LightCount;
    uint ClustersOffset;
    uint IndicesOffset;
};

struct Light
{
    float3 Position;
    float Range;
    float3 Color;
    float CosOuter;
    float3 Direction;
    float CosInner;
};

[[vk::push_constant]] ConstantBuffer<SceneConstants> Scene : register(b0, space0);

static const uint VertexStride = 32;
static const uint FrameDataSize = 128;
static const uint LightSize = 48;
// The cluster grid of LightClusters.
static const uint TilesX = 16;
static const uint TilesY = 9;
static const uint Slices = 24;

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 WorldPosition : POSITION;
    float3 Normal : NORMAL;
    float2 Uv : TEXCOORD0;
};

FrameData LoadFrame()
{
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    FrameData frame;
    // Rows of a System.Numerics matrix, which multiplies row vectors.
    frame.ViewProjection = float4x4(buffer.Load<float4>(0), buffer.Load<float4>(16), buffer.Load<float4>(32), buffer.Load<float4>(48));
    frame.DepthAxis = buffer.Load<float4>(64);
    frame.SunDirection = buffer.Load<float3>(80);
    frame.Ambient = buffer.Load<float>(92);
    frame.TargetSize = buffer.Load<float2>(96);
    frame.SliceScale = buffer.Load<float>(104);
    frame.SliceBias = buffer.Load<float>(108);
    frame.LogarithmicSlices = buffer.Load<uint>(112);
    frame.LightCount = buffer.Load<uint>(116);
    frame.ClustersOffset = buffer.Load<uint>(120);
    frame.IndicesOffset = buffer.Load<uint>(124);
    return frame;
}

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    FrameData frame = LoadFrame();
    ByteAddressBuffer vertices = Buffers[Scene.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);
    float3 normal = vertices.Load<float3>(offset + 12);
    float4 world = mul(float4(position, 1), Scene.World);

    // The cofactor matrix keeps normals at right angles to surfaces under non-uniform scale; mirroring flips its sign.
    float3x3 basis = (float3x3)Scene.World;
    float3x3 cofactor = float3x3(cross(basis[1], basis[2]), cross(basis[2], basis[0]), cross(basis[0], basis[1]));
    float mirror = sign(dot(basis[0], cofactor[0]));

    VertexOutput output;
    output.Position = mul(world, frame.ViewProjection);
    output.WorldPosition = world.xyz;
    output.Normal = mul(normal, cofactor) * mirror;
    output.Uv = vertices.Load<float2>(offset + 24);
    return output;
}

uint FindCluster(FrameData frame, float2 pixel, float3 worldPosition)
{
    uint2 tile = min(uint2(pixel * float2(TilesX, TilesY) / frame.TargetSize), uint2(TilesX - 1, TilesY - 1));
    float depth = dot(float4(worldPosition, 1), frame.DepthAxis);
    if (frame.LogarithmicSlices != 0)
    {
        depth = log(max(depth, 1e-6));
    }

    float slice = clamp(floor(depth * frame.SliceScale + frame.SliceBias), 0, Slices - 1);
    return ((uint)slice * TilesY + tile.y) * TilesX + tile.x;
}

// Inverse-square falloff, softened near the light and windowed to reach zero at its range.
float3 ShadeLight(Light light, float3 worldPosition, float3 normal)
{
    float3 toLight = light.Position - worldPosition;
    float distanceSquared = dot(toLight, toLight);
    float3 direction = toLight * rsqrt(max(distanceSquared, 1e-8));
    float ratio = distanceSquared / (light.Range * light.Range);
    float window = saturate(1 - ratio * ratio);
    float falloff = window * window / (distanceSquared + 1);
    float cone = smoothstep(light.CosOuter, light.CosInner, dot(-direction, light.Direction));
    float facing = saturate(dot(normal, direction));
    return light.Color * (facing * falloff * cone);
}

float3 ShadeClusterLights(FrameData frame, float2 pixel, float3 worldPosition, float3 normal)
{
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    uint cluster = FindCluster(frame, pixel, worldPosition);
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    float3 light = 0;
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light clusterLight = buffer.Load<Light>(FrameDataSize + lightIndex * LightSize);
        light += ShadeLight(clusterLight, worldPosition, normal);
    }

    return light;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    FrameData frame = LoadFrame();
    float4 albedo = Textures[Scene.Texture].Sample(Samplers[Scene.Sampler], input.Uv) * Scene.BaseColor;
    float3 normal = normalize(input.Normal);
    float sun = saturate(dot(normal, -normalize(frame.SunDirection)));
    float3 light = frame.Ambient + (1 - frame.Ambient) * sun;
    light += ShadeClusterLights(frame, input.Position.xy, input.WorldPosition, normal);
    return float4(albedo.rgb * light, albedo.a);
}
