using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds a 16x16 cube capture whose radiance is a function of direction, and projects it.</summary>
internal sealed class CubeCaptureTestsBuilder
{
    public const int Size = 16;

    private Func<Vector3, Vector3> _radiance = static _ => Vector3.Zero;

    public CubeCaptureTestsBuilder WithRadiance(Func<Vector3, Vector3> radiance)
    {
        _radiance = radiance;
        return this;
    }

    public IrradianceProbe Project()
    {
        Vector4[] pixels = new Vector4[CubeCapture.Faces * Size * Size];
        for (int face = 0; face < CubeCapture.Faces; face++)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector3 direction = CubeCapture.GetDirection(face, x, y, Size);
                    Vector3 radiance = _radiance(direction);
                    pixels[(((face * Size) + y) * Size) + x] = new Vector4(radiance, 1);
                }
            }
        }

        return CubeCapture.Project(pixels, Size);
    }
}
