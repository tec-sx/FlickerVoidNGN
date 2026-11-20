// Poses a skinned mesh part: each thread takes one bind-pose vertex (float3 position, float3 normal, float2 uv) and its skin
// (uint4 joints, float4 weights), blends the matrices of its joints by their weights, and writes the posed vertex, which
// every later pass draws like a rigid one.

ByteAddressBuffer Buffers[] : register(t0, space2);
RWByteAddressBuffer RWBuffers[] : register(u0, space2);

struct SkinningConstants
{
    uint Source;
    uint Skin;
    uint Joints;
    uint JointsOffset;
    uint Output;
    uint VertexCount;
};

[[vk::push_constant]] ConstantBuffer<SkinningConstants> Skinning : register(b0, space0);

static const uint VertexStride = 32;
static const uint SkinStride = 32;
static const uint MatrixSize = 64;

float4x4 LoadJoint(ByteAddressBuffer joints, uint joint)
{
    // Rows of a System.Numerics matrix, which multiplies row vectors.
    uint offset = Skinning.JointsOffset + (joint * MatrixSize);
    return float4x4(joints.Load<float4>(offset), joints.Load<float4>(offset + 16), joints.Load<float4>(offset + 32), joints.Load<float4>(offset + 48));
}

[numthreads(64, 1, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= Skinning.VertexCount)
    {
        return;
    }

    ByteAddressBuffer source = Buffers[Skinning.Source];
    ByteAddressBuffer skins = Buffers[Skinning.Skin];
    ByteAddressBuffer joints = Buffers[Skinning.Joints];
    uint offset = id.x * VertexStride;
    float3 position = source.Load<float3>(offset);
    float3 normal = source.Load<float3>(offset + 12);
    float2 uv = source.Load<float2>(offset + 24);
    uint4 indices = skins.Load<uint4>(id.x * SkinStride);
    float4 weights = skins.Load<float4>((id.x * SkinStride) + 16);

    float4x4 skin = LoadJoint(joints, indices.x) * weights.x;
    skin += LoadJoint(joints, indices.y) * weights.y;
    skin += LoadJoint(joints, indices.z) * weights.z;
    skin += LoadJoint(joints, indices.w) * weights.w;

    float3 posed = mul(float4(position, 1), skin).xyz;
    float3 posedNormal = mul(normal, (float3x3)skin);
    float normalLength = length(posedNormal);
    if (normalLength > 0)
    {
        posedNormal /= normalLength;
    }

    RWByteAddressBuffer output = RWBuffers[Skinning.Output];
    output.Store<float3>(offset, posed);
    output.Store<float3>(offset + 12, posedNormal);
    output.Store<float2>(offset + 24, uv);
}
