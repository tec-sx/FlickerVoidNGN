// How the G-buffer's targets (GBuffer.cs) hold a surface, shared by the geometry pass that writes them and the passes that read
// them, and the MeshFlags its material target carries.

#pragma once

// MeshFlags.Uncut: the cutaway keeps the mesh, such as a door.
static const uint FlagUncut = 4;

// MeshFlags.Greyed: the mesh is drawn black and white, as the editor's creator greys the rest of a level.
static const uint FlagGreyed = 8;

// A unit vector folded onto an octahedron's square, so two channels hold it (Cigolle et al. 2014, "A Survey of Efficient
// Representations for Independent Unit Vectors").
float2 EncodeNormal(float3 normal)
{
    float2 folded = normal.xy / (abs(normal.x) + abs(normal.y) + abs(normal.z));
    if (normal.z < 0)
    {
        float2 signs = select(folded >= 0, float2(1, 1), float2(-1, -1));
        folded = (1 - abs(folded.yx)) * signs;
    }

    return folded;
}

// Unfolds a normal EncodeNormal folded.
float3 DecodeNormal(float2 folded)
{
    float3 normal = float3(folded, 1 - abs(folded.x) - abs(folded.y));
    float fold = saturate(-normal.z);
    float2 signs = select(normal.xy >= 0, float2(-1, -1), float2(1, 1));
    normal.xy += signs * fold;
    return normalize(normal);
}

// The material target's four bytes: the low and high byte of the material's bindless index, how thick the paint stands and the
// mesh's flags.
float4 EncodeMaterial(uint material, float thickness, uint flags)
{
    float2 index = float2(material & 0xFF, (material >> 8) & 0xFF) / 255;
    return float4(index, thickness, float(flags & 0xFF) / 255);
}

// What the material target holds at a pixel.
struct SurfaceMaterial
{
    uint Material;
    float Thickness;
    uint Flags;
};

SurfaceMaterial DecodeMaterial(float4 bytes)
{
    uint4 values = uint4(round(bytes * 255));
    SurfaceMaterial decoded;
    decoded.Material = values.x | (values.y << 8);
    decoded.Thickness = bytes.z;
    decoded.Flags = values.w;
    return decoded;
}
