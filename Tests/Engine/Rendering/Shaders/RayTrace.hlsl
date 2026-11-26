// Traces rays against the scene's acceleration structure the frame data names, for SceneRendererTests: each ray an origin and a
// direction (float4s), each hit the number of the draw hit first, or ~0 on a miss, how far along the ray, and where the mesh of
// the draw record with that number stands (its world's translation). The content build keeps a folder's shaders to its own files,
// so this one reads FrameData.DrawsOffset and FrameData.AccelerationStructure at their offsets in Frame.hlsl and a draw's world at
// its offset in Scene.hlsl.

RaytracingAccelerationStructure AccelerationStructures[] : register(t0, space5);
ByteAddressBuffer Buffers[] : register(t0, space2);
RWByteAddressBuffer RWBuffers[] : register(u0, space2);

static const uint DrawsOffsetField = 480;
static const uint AccelerationStructureField = 508;
static const uint DrawSize = 208;
static const uint DrawTranslation = 48;
static const uint RaySize = 32;
static const uint HitSize = 32;

struct TraceConstants
{
    uint Frame;
    uint Rays;
    uint Hits;
    uint Count;
};

[[vk::push_constant]] ConstantBuffer<TraceConstants> Trace : register(b0, space0);

[numthreads(64, 1, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= Trace.Count)
    {
        return;
    }

    ByteAddressBuffer frame = Buffers[Trace.Frame];
    uint scene = frame.Load<uint>(AccelerationStructureField);
    RayDesc ray;
    ray.Origin = Buffers[Trace.Rays].Load<float4>(id.x * RaySize).xyz;
    ray.Direction = Buffers[Trace.Rays].Load<float4>(id.x * RaySize + 16).xyz;
    ray.TMin = 0;
    ray.TMax = 1000;
    RayQuery<RAY_FLAG_NONE> query;
    query.TraceRayInline(AccelerationStructures[scene], RAY_FLAG_NONE, 0xFF, ray);
    while (query.Proceed())
    {
    }

    uint draw = 0xFFFFFFFF;
    float distance = 0;
    float3 translation = 0;
    if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        draw = query.CommittedInstanceID();
        distance = query.CommittedRayT();
        uint record = frame.Load<uint>(DrawsOffsetField) + draw * DrawSize;
        translation = frame.Load<float3>(record + DrawTranslation);
    }

    RWBuffers[Trace.Hits].Store4(id.x * HitSize, uint4(draw, asuint(distance), asuint(translation.xy)));
    RWBuffers[Trace.Hits].Store(id.x * HitSize + 16, asuint(translation.z));
}
