// Culls the copies of an instance batch one by one, a thread a copy (CullMain): a copy whose sphere (the model's, sized as the
// copy is) lies wholly outside one of the view's planes is left out, the rest add their number to the visible list, counted in
// the arguments' first word. FinishMain then gives that count to each of the model's parts' indirect draw arguments (five words
// each, after the count), adds it to the frame's tally and clears the count for the next frame. ClearMain clears the tally.

#include "Frame.hlsl"
#include "Instance.hlsl"

RWByteAddressBuffer RWBuffers[] : register(u0, space2);

struct CullConstants
{
    uint Frame;
    uint Copies;
    uint Visible;
    uint Arguments;
    uint Count;
    uint Parts;
    uint Counted;
    uint Scales;
    // The sphere round the model, before a copy places it.
    float3 Center;
    float Radius;
};

[[vk::push_constant]] ConstantBuffer<CullConstants> Pass : register(b0, space0);

static const uint ArgumentsSize = 20;

static const uint InstanceCountOffset = 4;

// Whether the plane (facing in, unnormalized) leaves any of the sphere on its inner side.
bool IsInside(float4 plane, float3 center, float radius)
{
    return dot(plane.xyz, center) + plane.w >= -radius * length(plane.xyz);
}

// Whether any of the sphere may be in view, by the planes of a view-projection for row vectors with depth from 0 to 1, as
// Runtime.Geometry.Frustum finds them.
bool IsInView(float4x4 viewProjection, float3 center, float radius)
{
    float4x4 columns = transpose(viewProjection);
    float4 x = columns[0];
    float4 y = columns[1];
    float4 z = columns[2];
    float4 w = columns[3];
    return IsInside(w + x, center, radius) && IsInside(w - x, center, radius) && IsInside(w + y, center, radius)
        && IsInside(w - y, center, radius) && IsInside(z, center, radius) && IsInside(w - z, center, radius);
}

[numthreads(64, 1, 1)]
void CullMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= Pass.Count)
    {
        return;
    }

    FrameData frame = LoadFrame(Pass.Frame);
    Instance copy = LoadInstance(Buffers[Pass.Copies], id.x);
    float size = InstanceScale(copy, Pass.Scales);
    float4x4 world = InstanceWorld(copy, size);
    float3 center = mul(float4(Pass.Center, 1), world).xyz;
    if (!IsInView(frame.ViewProjection, center, Pass.Radius * size))
    {
        return;
    }

    uint slot;
    RWBuffers[Pass.Arguments].InterlockedAdd(0, 1, slot);
    RWBuffers[Pass.Visible].Store(slot * 4, id.x);
}

[numthreads(1, 1, 1)]
void FinishMain()
{
    RWByteAddressBuffer arguments = RWBuffers[Pass.Arguments];
    uint count = arguments.Load(0);
    for (uint part = 0; part < Pass.Parts; part++)
    {
        arguments.Store(4 + part * ArgumentsSize + InstanceCountOffset, count);
    }

    arguments.Store(0, 0);
    uint before;
    RWBuffers[Pass.Counted].InterlockedAdd(0, count, before);
}

[numthreads(1, 1, 1)]
void ClearMain()
{
    RWBuffers[Pass.Counted].Store(0, 0);
}
