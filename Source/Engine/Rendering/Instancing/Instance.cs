using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>
/// One copy in an <see cref="InstanceBatch"/>, in 16 bytes as the GPU reads it (Instance.hlsl): where it stands in the world, then
/// one word packing its turn about the vertical (256 steps of a whole turn), its size as a share of the batch's scale range, its
/// variant and its phase in the wind, a byte each.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Instance
{
    private const float Steps = 256;

    private readonly Vector3 _position;
    private readonly uint _packed;

    /// <summary>Packs a copy: <paramref name="turn"/> in radians, any angle; <paramref name="scale"/> from 0 (the batch's smallest) to 1 (its largest).</summary>
    public Instance(Vector3 position, float turn, float scale, byte variant, byte windPhase)
    {
        _position = position;
        float wholeTurns = turn / MathF.Tau;
        float share = wholeTurns - MathF.Floor(wholeTurns);
        uint turnStep = (uint)MathF.Round(share * Steps) & 0xFF;
        uint scaleStep = (uint)MathF.Round(Math.Clamp(scale, 0, 1) * 255);
        _packed = turnStep | scaleStep << 8 | (uint)variant << 16 | (uint)windPhase << 24;
    }

    public Vector3 Position => _position;

    /// <summary>The turn about the vertical, in radians from 0 up to a whole turn.</summary>
    public float Turn => (_packed & 0xFF) / Steps * MathF.Tau;

    /// <summary>The size as a share of the batch's scale range, from 0 to 1.</summary>
    public float Scale => (_packed >> 8 & 0xFF) / 255f;

    public byte Variant => (byte)(_packed >> 16);

    public byte WindPhase => (byte)(_packed >> 24);

    /// <summary>Where the copy places its model: sized within the range, turned about the vertical, then moved to its position.</summary>
    public Matrix4x4 ToMatrix(float minScale, float maxScale)
    {
        float size = float.Lerp(minScale, maxScale, Scale);
        Matrix4x4 scale = Matrix4x4.CreateScale(size);
        Matrix4x4 turn = Matrix4x4.CreateRotationY(Turn);
        Matrix4x4 sized = scale * turn;
        sized.Translation = Position;
        return sized;
    }
}
