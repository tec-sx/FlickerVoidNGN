using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// The six 90 degree views a probe captures its surroundings with (+X, -X, +Y, -Y, +Z, -Z), and the projection of the
/// captured radiance onto an <see cref="IrradianceProbe"/>.
/// </summary>
public static class CubeCapture
{
    public const int Faces = 6;

    // Real spherical harmonics constants: Y00, and Y1m divided by the direction's component.
    private const float ConstantBasis = 0.282095f;
    private const float LinearBasis = 0.488603f;

    // The cosine lobe convolution divided by pi: 1 for the constant band, 2/3 for the linear one.
    private const float LinearConvolution = 2f / 3f;

    private static readonly Vector3[] Forwards = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
    private static readonly Vector3[] Ups = [Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitY];

    /// <summary>The rotation of a camera that looks down the face's axis (cameras look along their -Z).</summary>
    public static Quaternion GetFaceRotation(int face)
    {
        Matrix4x4 world = Matrix4x4.CreateWorld(Vector3.Zero, Forwards[face], Ups[face]);
        return Quaternion.CreateFromRotationMatrix(world);
    }

    /// <summary>The direction through the centre of a texel of a face rendered size by size, rows counted from the top.</summary>
    public static Vector3 GetDirection(int face, int x, int y, int size)
    {
        Vector3 local = GetLocalDirection(x, y, size);
        Quaternion rotation = GetFaceRotation(face);
        Vector3 direction = Vector3.Transform(local, rotation);
        return Vector3.Normalize(direction);
    }

    /// <summary>Projects the radiance of all six faces, face after face, each size by size with its top row first.</summary>
    public static IrradianceProbe Project(ReadOnlySpan<Vector4> pixels, int size)
    {
        Vector3 constant = Vector3.Zero;
        Vector3 x = Vector3.Zero;
        Vector3 y = Vector3.Zero;
        Vector3 z = Vector3.Zero;
        float totalWeight = 0;
        for (int face = 0; face < Faces; face++)
        {
            Quaternion rotation = GetFaceRotation(face);
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    Vector3 local = GetLocalDirection(column, row, size);

                    // A texel's solid angle shrinks toward the face's corners, with the cube of its distance from the centre.
                    float distanceSquared = local.LengthSquared();
                    float weight = 1 / (distanceSquared * MathF.Sqrt(distanceSquared));
                    Vector3 rotated = Vector3.Transform(local, rotation);
                    Vector3 direction = Vector3.Normalize(rotated);
                    Vector4 pixel = pixels[(((face * size) + row) * size) + column];
                    Vector3 radiance = new Vector3(pixel.X, pixel.Y, pixel.Z) * weight;
                    constant += radiance;
                    x += radiance * direction.X;
                    y += radiance * direction.Y;
                    z += radiance * direction.Z;
                    totalWeight += weight;
                }
            }
        }

        // The weights sum to the whole sphere, 4 pi steradians.
        float toSolidAngle = 4 * MathF.PI / totalWeight;
        Vector3 constantTerm = constant * (toSolidAngle * ConstantBasis * ConstantBasis);
        float linearScale = toSolidAngle * LinearBasis * LinearBasis * LinearConvolution;
        Vector3 xTerm = x * linearScale;
        Vector3 yTerm = y * linearScale;
        Vector3 zTerm = z * linearScale;
        Vector4 red = new(constantTerm.X, xTerm.X, yTerm.X, zTerm.X);
        Vector4 green = new(constantTerm.Y, xTerm.Y, yTerm.Y, zTerm.Y);
        Vector4 blue = new(constantTerm.Z, xTerm.Z, yTerm.Z, zTerm.Z);
        return new IrradianceProbe(red, green, blue);
    }

    private static Vector3 GetLocalDirection(int x, int y, int size)
    {
        float u = ((2 * (x + 0.5f)) / size) - 1;
        float v = 1 - ((2 * (y + 0.5f)) / size);
        return new Vector3(u, v, -1);
    }
}
