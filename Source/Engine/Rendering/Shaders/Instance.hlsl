// A copy in an instance batch (Instancing/Instance.cs), 16 bytes in its batch's buffer: where it stands, then one word packing its
// turn about the vertical (256 steps of a whole turn), its size as a share of the batch's scale range, its variant and its wind
// phase, a byte each. A copy stands upright, sized, turned, then moved, as Instance.ToMatrix places it.

#pragma once

static const uint InstanceSize = 16;

struct Instance
{
    float3 Position;
    float Turn;
    float Scale;
    uint Variant;
    uint WindPhase;
};

Instance LoadInstance(ByteAddressBuffer copies, uint index)
{
    uint offset = index * InstanceSize;
    uint packed = copies.Load<uint>(offset + 12);
    Instance instance;
    instance.Position = copies.Load<float3>(offset);
    instance.Turn = (packed & 0xFF) / 256.0 * 6.28318530718;
    instance.Scale = ((packed >> 8) & 0xFF) / 255.0;
    instance.Variant = (packed >> 16) & 0xFF;
    instance.WindPhase = packed >> 24;
    return instance;
}

// The copy's size between the batch's smallest and largest, given as two halves (Draw.Scales).
float InstanceScale(Instance instance, uint scales)
{
    float smallest = f16tof32(scales & 0xFFFF);
    float largest = f16tof32(scales >> 16);
    return lerp(smallest, largest, instance.Scale);
}

// Where the copy places its model, in rows for row vectors as System.Numerics' Matrix4x4.CreateScale * CreateRotationY with the
// translation set.
float4x4 InstanceWorld(Instance instance, float size)
{
    float c = cos(instance.Turn) * size;
    float s = sin(instance.Turn) * size;
    return float4x4(
        float4(c, 0, -s, 0),
        float4(0, size, 0, 0),
        float4(s, 0, c, 0),
        float4(instance.Position, 1));
}
