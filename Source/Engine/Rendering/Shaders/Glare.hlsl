// The lens flare's glare, after Arkham Knight by way of Froyok: one point per 2x2 block of the flare's threshold, five taps; a
// point bright enough is appended to a list (FindMain, after the list's count is copied to zero), and one instanced draw turns each into a star
// of three stretched quads, 60 degrees apart, added to the flare (VertexMain, PixelMain). Instances past the list's count
// collapse to nothing, so no indirect draw is needed.

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
RWByteAddressBuffer RWBuffers[] : register(u0, space2);

struct GlareConstants
{
    uint Source;
    uint Points;
    uint Width;
    uint Height;
    float Threshold;
    float Intensity;
    float Length;
    float StreakWidth;
    float Angle;
    float Aspect;
};

[[vk::push_constant]] ConstantBuffer<GlareConstants> Pass : register(b0, space0);

static const float3 ToLuminance = float3(0.2126, 0.7152, 0.0722);

// The list: a count, padded to 16 bytes, then each point's uv and colour in 32 bytes.
static const uint PointsOffset = 16;
static const uint PointStride = 32;
static const uint MaxPoints = 1024;
static const uint Streaks = 3;
static const float Pi = 3.14159265;

float3 Tap(int2 pixel)
{
    int2 clamped = clamp(pixel, int2(0, 0), int2(Pass.Width - 1, Pass.Height - 1));
    return Textures[Pass.Source].Load(int3(clamped, 0)).rgb;
}

[numthreads(8, 8, 1)]
void FindMain(uint3 id : SV_DispatchThreadID)
{
    int2 block = id.xy * 2;
    if (any(block >= int2(Pass.Width, Pass.Height)))
    {
        return;
    }

    float3 light = Tap(block) + Tap(block + int2(1, 0)) + Tap(block + int2(0, 1)) + Tap(block + int2(1, 1)) + Tap(block + int2(-1, -1));
    light /= 5;
    if (dot(light, ToLuminance) <= Pass.Threshold)
    {
        return;
    }

    uint index;
    RWBuffers[Pass.Points].InterlockedAdd(0, 1, index);
    if (index >= MaxPoints)
    {
        return;
    }

    float2 uv = (block + 1) / float2(Pass.Width, Pass.Height);
    uint offset = PointsOffset + index * PointStride;
    RWBuffers[Pass.Points].Store4(offset, asuint(float4(uv, 0, 0)));
    RWBuffers[Pass.Points].Store4(offset + 16, asuint(float4(light * Pass.Intensity, 0)));
}

// Two triangles from -1 to 1 along and across a streak.
static const float2 Corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

struct Streak
{
    float4 Position : SV_Position;
    float2 Along : TEXCOORD0;
    float3 Color : COLOR0;
};

Streak VertexMain(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    Streak streak = (Streak)0;
    uint pointIndex = instanceId / Streaks;
    uint count = min(Buffers[Pass.Points].Load(0), MaxPoints);
    if (pointIndex >= count)
    {
        streak.Position = float4(2, 2, 0, 1);
        return streak;
    }

    uint offset = PointsOffset + pointIndex * PointStride;
    float2 uv = asfloat(Buffers[Pass.Points].Load2(offset));
    float3 color = asfloat(Buffers[Pass.Points].Load3(offset + 16));

    float2 corner = Corners[vertexId];
    float angle = radians(Pass.Angle) + (instanceId % Streaks) * Pi / Streaks;
    float2 along = float2(cos(angle), sin(angle));
    float2 across = float2(-along.y, along.x);

    // In screen heights, then into uv by the aspect, then into clip space with y down.
    float2 offsetHeights = along * corner.x * Pass.Length + across * corner.y * Pass.StreakWidth;
    float2 position = uv + offsetHeights * float2(1 / Pass.Aspect, 1);
    streak.Position = float4(position.x * 2 - 1, 1 - position.y * 2, 0, 1);
    streak.Along = corner;
    streak.Color = color;
    return streak;
}

float4 PixelMain(Streak streak) : SV_Target
{
    float across = exp(-streak.Along.y * streak.Along.y * 4);
    float fade = 1 - abs(streak.Along.x);
    return float4(streak.Color * across * fade * fade, 0);
}
