// Beams of sunlight the renderer places by itself where light breaks through a gap, while the frame traces rays. UpdateMain
// keeps a beam on each seed of a 16 by 16 grid laid on the world around the focus, a thread a seed: the seed's slot follows its
// cell round the grid, so a seed keeps its beam while it stays in reach and the beams stand still as the focus moves. Each seed
// finds the ground with a ray straight down from a little above the focus, so it lands on the floor under a roof, not on the
// roof; from points on a disk round it, rays go toward the sun, and the share of them blocked sets how much of a beam stands
// there (none in full sun or full shade, most at half) and the distance to what blocks them how tall it is, both eased toward
// over the frames. DrawMain adds the beams to the lit scene, a thread a pixel: each beam is a soft column from the ground toward
// the sun, brightest along its axis and thinning to nothing at its edge and its top, fading where it passes behind the scene,
// in the sun's light times the mood's beam colour and intensity. Each 8 by 8 tile first gathers the beams near it.

#include "Frame.hlsl"
#include "RayTracing.hlsl"

RWByteAddressBuffer RWBuffers[] : register(u0, space2);

struct LightBeamConstants
{
    // The focus beams gather round, and the distance between two seeds.
    float3 Center;
    float Spacing;
    // The beams' light, as a share of the sun's.
    float3 Color;
    float HalfWidth;
    // The grid cell holding the focus.
    int2 Cell;
    // How far this frame moves each beam toward its seed's strength and height; 1 sets them outright.
    float Blend;
    float Lift;
    uint Frame;
    uint Beams;
    uint Output;
    uint Padding;
};

[[vk::push_constant]] ConstantBuffer<LightBeamConstants> Pass : register(b0, space0);

static const int GridSize = 16;
static const uint BeamCount = GridSize * GridSize;
static const uint BeamSize = 32;
static const uint SunRays = 16;
// How far below the lift the ground ray looks, and how far toward the sun a blocker can stand.
static const float GroundReach = 20;
static const float SunReach = 40;
static const float GoldenAngle = 2.39996323;

// The cell a slot holds: the one within half the grid of the focus's cell that the slot's place round the grid falls on.
int SlotCell(int slot, int focusCell)
{
    int first = focusCell - GridSize / 2;
    return first + ((slot - first) % GridSize + GridSize) % GridSize;
}

// How much of a beam stands where the given share of the sun's rays is blocked: none at none or all, most at half.
float BeamStrength(float blocked)
{
    return saturate(4 * blocked * (1 - blocked));
}

[numthreads(64, 1, 1)]
void UpdateMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= BeamCount)
    {
        return;
    }

    FrameData frame = LoadFrame(Pass.Frame);
    int2 slot = int2(id.x % GridSize, id.x / GridSize);
    int2 cell = int2(SlotCell(slot.x, Pass.Cell.x), SlotCell(slot.y, Pass.Cell.y));
    uint seed = Hash(asuint(cell.x) + Hash(asuint(cell.y)));
    float2 jitter = float2(HashToUnit(seed), HashToUnit(Hash(seed))) - 0.5;
    float2 place = (float2(cell) + 0.5 + jitter * 0.6) * Pass.Spacing;
    float3 above = float3(place.x, Pass.Center.y + Pass.Lift, place.y);
    RayHit ground = TraceClosest(frame, above, float3(0, -1, 0), Pass.Lift + GroundReach);
    float3 toSun = -normalize(frame.SunDirection);
    float target = 0;
    float height = 0;
    float3 groundPoint = above;
    if (ground.Hit && toSun.y > 0.02)
    {
        groundPoint = above - float3(0, ground.Distance - 0.02, 0);
        float radius = Pass.Spacing * 0.75;
        uint blocked = 0;
        float blockerDistance = 0;
        for (uint ray = 0; ray < SunRays; ray++)
        {
            float angle = ray * GoldenAngle;
            float reach = radius * sqrt((ray + 0.5) / SunRays);
            float3 origin = groundPoint + float3(cos(angle), 0, sin(angle)) * reach;
            RayHit hit = TraceClosest(frame, origin, toSun, SunReach);
            if (hit.Hit)
            {
                blocked++;
                blockerDistance += hit.Distance;
            }
        }

        float share = float(blocked) / SunRays;
        target = BeamStrength(share);
        if (blocked > 0)
        {
            height = blockerDistance / blocked;
        }
    }

    uint offset = id.x * BeamSize;
    RWByteAddressBuffer beams = RWBuffers[Pass.Beams];
    float4 last = beams.Load<float4>(offset);
    float4 lastShape = beams.Load<float4>(offset + 16);
    bool sameSeed = all(asint(lastShape.yz) == cell);
    float strength = target;
    if (Pass.Blend < 1)
    {
        float from = 0;
        float fromHeight = height;
        if (sameSeed)
        {
            from = last.w;
            fromHeight = lastShape.x;
        }

        strength = lerp(from, target, Pass.Blend);
        if (target > 0)
        {
            height = lerp(fromHeight, height, Pass.Blend);
        }
        else
        {
            height = fromHeight;
        }
    }

    beams.Store<float4>(offset, float4(groundPoint, strength));
    beams.Store<float4>(offset + 16, float4(height, asfloat(cell.x), asfloat(cell.y), 0));
}

// A beam as DrawMain reads it: where it stands, which way it rises and how far, and how strong it is.
struct Beam
{
    float3 Ground;
    float Strength;
    float Height;
};

Beam LoadBeam(uint index)
{
    ByteAddressBuffer beams = Buffers[Pass.Beams];
    float4 ground = beams.Load<float4>(index * BeamSize);
    float4 shape = beams.Load<float4>(index * BeamSize + 16);
    Beam beam;
    beam.Ground = ground.xyz;
    beam.Strength = ground.w;
    beam.Height = shape.x;
    return beam;
}

// The nearest the view ray from the origin along the unit direction comes to the beam's axis: how far from the axis (x), how far
// along the ray (y) and how far up the beam (z).
float3 ClosestApproach(float3 origin, float3 direction, Beam beam, float3 toSun)
{
    float3 fromGround = origin - beam.Ground;
    float alongSun = dot(direction, toSun);
    float rayOffset = dot(direction, fromGround);
    float sunOffset = dot(toSun, fromGround);
    float denominator = 1 - alongSun * alongSun;
    float up = 0;
    if (denominator > 1e-5)
    {
        float t = (alongSun * sunOffset - rayOffset) / denominator;
        up = sunOffset + t * alongSun;
    }

    up = clamp(up, 0, beam.Height);
    float along = max(up * alongSun - rayOffset, 0);
    float3 onRay = origin + direction * along;
    float3 onBeam = beam.Ground + toSun * up;
    return float3(length(onRay - onBeam), along, up);
}

// The view ray through a pixel: where it starts on the near plane and which way it goes.
float3 ViewRay(FrameData frame, float2 pixel, out float3 direction)
{
    float3 nearPoint = WorldAtPixel(frame, pixel, 0);
    float3 farPoint = WorldAtPixel(frame, pixel, 1);
    direction = normalize(farPoint - nearPoint);
    return nearPoint;
}

groupshared uint TileBeams[BeamCount];
groupshared uint TileBeamCount;

[numthreads(8, 8, 1)]
void DrawMain(uint3 id : SV_DispatchThreadID, uint3 group : SV_GroupID, uint index : SV_GroupIndex)
{
    FrameData frame = LoadFrame(Pass.Frame);
    float3 toSun = -normalize(frame.SunDirection);
    if (index == 0)
    {
        TileBeamCount = 0;
    }

    GroupMemoryBarrierWithGroupSync();
    float2 tileCorner = float2(group.xy * 8);
    float3 centerDirection;
    float3 centerOrigin = ViewRay(frame, tileCorner + 4, centerDirection);
    float3 cornerDirection;
    float3 cornerOrigin = ViewRay(frame, tileCorner, cornerDirection);
    for (uint candidate = index; candidate < BeamCount; candidate += 64)
    {
        Beam beam = LoadBeam(candidate);
        if (beam.Strength <= 1e-3 || beam.Height <= 0)
        {
            continue;
        }

        float3 approach = ClosestApproach(centerOrigin, centerDirection, beam, toSun);
        float3 cornerPoint = cornerOrigin + cornerDirection * approach.y;
        float3 centerPoint = centerOrigin + centerDirection * approach.y;
        float tileRadius = length(cornerPoint - centerPoint);
        if (approach.x < Pass.HalfWidth + tileRadius)
        {
            uint slot;
            InterlockedAdd(TileBeamCount, 1, slot);
            TileBeams[slot] = candidate;
        }
    }

    GroupMemoryBarrierWithGroupSync();
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    float2 pixel = float2(id.xy) + 0.5;
    float3 direction;
    float3 origin = ViewRay(frame, pixel, direction);
    float depth = Textures[frame.Depth].Load(int3(id.xy, 0)).r;
    float sceneDistance = 1e9;
    if (depth < 1)
    {
        sceneDistance = length(WorldAtPixel(frame, pixel, depth) - origin);
    }

    float light = 0;
    for (uint entry = 0; entry < TileBeamCount; entry++)
    {
        Beam beam = LoadBeam(TileBeams[entry]);
        float3 approach = ClosestApproach(origin, direction, beam, toSun);
        float across = saturate(1 - approach.x * approach.x / (Pass.HalfWidth * Pass.HalfWidth));
        float rise = approach.z / beam.Height;
        float ends = smoothstep(0, 0.1, rise) * (1 - smoothstep(0.5, 1, rise));
        float inFront = saturate((sceneDistance - approach.y) / (Pass.HalfWidth * 2));
        light += beam.Strength * across * across * ends * inFront;
    }

    if (light <= 0)
    {
        return;
    }

    RWTexture2D<float4> output = RWTextures[Pass.Output];
    float4 color = output[id.xy];
    output[id.xy] = float4(color.rgb + light * Pass.Color * frame.SunLight, color.a);
}
