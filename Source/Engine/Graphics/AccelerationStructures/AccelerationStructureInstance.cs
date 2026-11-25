using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Graphics.AccelerationStructures;

/// <summary>
/// A bottom-level acceleration structure placed in a top-level one, as the GPU reads it from the buffer of instances a build is
/// given (64 bytes): where it stands, the id shaders see on a hit, and the mask rays select it by. Its triangles are opaque and
/// hit from both sides.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct AccelerationStructureInstance
{
    /// <summary>The largest id an instance can have: ids have 24 bits.</summary>
    public const uint MaxId = 0xFFFFFF;

    private const int MaskShift = 24;
    private const uint TriangleFacingCullDisable = 1u << 24;

    // The world matrix's first three columns as rows, the 3x4 matrix the GPU multiplies column vectors by.
    private readonly Vector4 _row0;
    private readonly Vector4 _row1;
    private readonly Vector4 _row2;
    private readonly uint _idAndMask;
    private readonly uint _offsetAndFlags;
    private readonly ulong _bottomLevel;

    /// <param name="world">Where it stands, a System.Numerics matrix (row vectors).</param>
    /// <param name="id">What shaders see as the instance's id on a hit, at most <see cref="MaxId"/>.</param>
    /// <param name="bottomLevel">The bottom-level structure placed.</param>
    /// <param name="mask">Rays whose mask shares no bit with it pass through it.</param>
    public AccelerationStructureInstance(in Matrix4x4 world, uint id, IAccelerationStructure bottomLevel, byte mask = 0xFF)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id, MaxId);
        _row0 = new Vector4(world.M11, world.M21, world.M31, world.M41);
        _row1 = new Vector4(world.M12, world.M22, world.M32, world.M42);
        _row2 = new Vector4(world.M13, world.M23, world.M33, world.M43);
        _idAndMask = id | ((uint)mask << MaskShift);
        _offsetAndFlags = TriangleFacingCullDisable;
        _bottomLevel = bottomLevel.Address;
    }
}
