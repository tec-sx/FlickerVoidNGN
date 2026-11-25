// The irradiance volume's probes as the frame's probe buffer holds them (IrradianceProbes.WriteTo): every probe's light, as
// first-order spherical harmonics already convolved for diffuse surfaces (IrradianceProbe.cs), then every probe's octahedral map
// of how far it sees, folded as the G-buffer folds normals, with the mean distance and mean squared distance of each texel's
// directions as shares of the probe's reach. A point is lit by the eight probes round it, each weighed by whether it can see the
// point, so light doesn't pass through walls (Majercik et al. 2019, "Dynamic Diffuse Global Illumination with Ray-Traced
// Irradiance Fields").

#pragma once

#include "Frame.hlsl"
#include "GBuffer.hlsl"

static const uint ProbeSize = 48;

// The width and height of each probe's map of distances (IrradianceProbes.DistanceSize), and the bytes of a texel.
static const uint DistanceSize = 16;

static const uint DistanceTexelSize = 8;

// How far the point a probe's sight is judged at stands off the surface, as a share of three quarters of the closest spacing of
// the probes: mostly toward the camera, a little along the normal, so a surface doesn't hide itself from its probes.
static const float SelfShadowBias = 0.3;

// Below this weight a probe's share is crushed further, so the light of probes that can hardly see a point fades out.
static const float CrushThreshold = 0.2;

uint ProbeCount(FrameData frame)
{
    return frame.ProbeCounts.x * frame.ProbeCounts.y * frame.ProbeCounts.z;
}

// The distance between neighbouring probes along each axis.
float3 ProbeSpacing(FrameData frame)
{
    return frame.ProbeSize / float3(frame.ProbeCounts);
}

// How far a probe's map of distances reaches, which its distances are shares of: 1.5 times the diagonal of a cell between probes.
float ProbeReach(FrameData frame)
{
    float3 spacing = ProbeSpacing(frame);
    return 1.5 * length(spacing);
}

// A probe's place in the buffer: x changing fastest, then y, then z.
uint ProbeIndex(FrameData frame, uint3 cell)
{
    return (cell.z * frame.ProbeCounts.y + cell.y) * frame.ProbeCounts.x + cell.x;
}

uint3 ProbeCell(FrameData frame, uint index)
{
    uint layer = frame.ProbeCounts.x * frame.ProbeCounts.y;
    uint z = index / layer;
    uint inLayer = index - z * layer;
    uint y = inLayer / frame.ProbeCounts.x;
    uint x = inLayer - y * frame.ProbeCounts.x;
    return uint3(x, y, z);
}

// Where a probe sits: in the middle of its cell of the volume (IrradianceVolume.GetProbePosition).
float3 ProbePosition(FrameData frame, uint3 cell)
{
    float3 spacing = ProbeSpacing(frame);
    return frame.ProbeMin + (float3(cell) + 0.5) * spacing;
}

// Where a texel of a probe's map of distances lies in the probe buffer, after every probe's light.
uint DistanceOffset(FrameData frame, uint probe, uint2 texel)
{
    uint distances = ProbeCount(frame) * ProbeSize;
    return distances + ((probe * DistanceSize + texel.y) * DistanceSize + texel.x) * DistanceTexelSize;
}

// The direction through the middle of a texel of a probe's map of distances.
float3 DistanceTexelDirection(uint2 texel)
{
    float2 folded = (float2(texel) + 0.5) / DistanceSize * 2 - 1;
    return DecodeNormal(folded);
}

// A probe's spherical harmonics turned into the irradiance (over pi) a surface facing the normal gets.
float3 EvaluateProbe(ByteAddressBuffer probes, uint index, float3 normal)
{
    uint offset = index * ProbeSize;
    float4 basis = float4(1, normal);
    float red = dot(probes.Load<float4>(offset), basis);
    float green = dot(probes.Load<float4>(offset + 16), basis);
    float blue = dot(probes.Load<float4>(offset + 32), basis);
    return max(float3(red, green, blue), 0);
}

// A texel of the probe's map of distances; a texel past an edge is the one the octahedron's faces meet it at, mirrored across
// that edge (and a corner's is the opposite corner).
float2 LoadDistance(FrameData frame, ByteAddressBuffer probes, uint probe, int2 texel)
{
    int last = DistanceSize - 1;
    if (texel.x < 0 || texel.x > last)
    {
        texel.x = clamp(texel.x, 0, last);
        texel.y = last - texel.y;
    }

    if (texel.y < 0 || texel.y > last)
    {
        texel.y = clamp(texel.y, 0, last);
        texel.x = last - texel.x;
    }

    uint offset = DistanceOffset(frame, probe, uint2(texel));
    return probes.Load<float2>(offset);
}

// The probe's mean distance and mean squared distance toward the unit direction, blended between the four texels round it.
float2 SampleDistance(FrameData frame, ByteAddressBuffer probes, uint probe, float3 direction)
{
    float2 folded = EncodeNormal(direction);
    float2 texel = (folded * 0.5 + 0.5) * DistanceSize - 0.5;
    float2 corner = floor(texel);
    float2 blend = texel - corner;
    int2 low = int2(corner);
    float2 topLeft = LoadDistance(frame, probes, probe, low);
    float2 topRight = LoadDistance(frame, probes, probe, low + int2(1, 0));
    float2 bottomLeft = LoadDistance(frame, probes, probe, low + int2(0, 1));
    float2 bottomRight = LoadDistance(frame, probes, probe, low + int2(1, 1));
    float2 top = lerp(topLeft, topRight, blend.x);
    float2 bottom = lerp(bottomLeft, bottomRight, blend.x);
    return lerp(top, bottom, blend.y);
}

// How likely the probe sees the point from its map of distances (Chebyshev's bound), when the point lies farther than the mean
// distance it sees in its direction; both as shares of the probe's reach.
float ProbeSight(FrameData frame, ByteAddressBuffer probes, uint probe, float3 probePosition, float3 seen, float reach)
{
    float3 fromProbe = seen - probePosition;
    float distance = length(fromProbe);
    float2 moments = SampleDistance(frame, probes, probe, fromProbe / max(distance, 1e-4));
    float share = distance / reach;
    if (share <= moments.x)
    {
        return 1;
    }

    float variance = abs(moments.x * moments.x - moments.y);
    float excess = share - moments.x;
    float bound = variance / (variance + excess * excess);
    return bound * bound * bound;
}

// The irradiance blended from the eight probes round the point, or nothing outside the volume. Each probe's share falls as the
// surface turns away from it and nearly vanishes where its map of distances says something stands between it and the point,
// judged a little off the surface, toward the camera (or the eye the point is seen from) and along the normal; a point in the
// air has no normal (0).
float3 SampleProbes(FrameData frame, float3 worldPosition, float3 normal, float3 towardCamera)
{
    if (frame.Probes == None)
    {
        return 0;
    }

    // Floors and walls a volume is fitted to stand on its sides, which IsInBox's margin keeps inside.
    float3 probeMax = frame.ProbeMin + frame.ProbeSize;
    if (!IsInBox(worldPosition, frame.ProbeMin, probeMax))
    {
        return 0;
    }

    float3 spacing = ProbeSpacing(frame);
    float closest = min(spacing.x, min(spacing.y, spacing.z));
    float3 offset = normal * 0.2 + towardCamera * 0.8;
    float3 biased = worldPosition + offset * (0.75 * closest * SelfShadowBias);
    float reach = ProbeReach(frame);
    ByteAddressBuffer probes = Buffers[frame.Probes];
    uint3 last = frame.ProbeCounts - 1;
    float3 grid = clamp((biased - frame.ProbeMin) / spacing - 0.5, 0, float3(last));
    uint3 low = min(uint3(grid), last);
    float3 blend = grid - float3(low);
    float3 irradiance = 0;
    float total = 0;
    for (uint corner = 0; corner < 8; corner++)
    {
        uint3 bits = uint3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
        uint3 cell = min(low + bits, last);
        float3 trilinear = lerp(1 - blend, blend, float3(bits));
        uint index = ProbeIndex(frame, cell);
        float3 probePosition = ProbePosition(frame, cell);

        // A soft turn away: a probe behind the surface still lends a little, so lighting doesn't break at its plane.
        float3 toProbe = probePosition - worldPosition;
        toProbe /= max(length(toProbe), 1e-4);
        float facing = (dot(toProbe, normal) + 1) * 0.5;
        float weight = facing * facing + 0.2;
        float sight = ProbeSight(frame, probes, index, probePosition, biased, reach);
        weight *= max(sight, 0.05);
        weight = max(weight, 1e-6);
        if (weight < CrushThreshold)
        {
            weight *= weight * weight / (CrushThreshold * CrushThreshold);
        }

        weight *= trilinear.x * trilinear.y * trilinear.z;
        irradiance += weight * EvaluateProbe(probes, index, normal);
        total += weight;
    }

    if (total <= 0)
    {
        return 0;
    }

    return irradiance / total;
}
