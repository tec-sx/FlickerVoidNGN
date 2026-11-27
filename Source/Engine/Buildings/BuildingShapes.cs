using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings;

/// <summary>The greyboxes a building's pieces start as: a storey's tray of floor and walls, a roof slab, a flight of stairs and an elevator's platform.</summary>
public static class BuildingShapes
{
    /// <summary>The rise of one step, at most, in metres: low enough for characters to walk up.</summary>
    public const float StepRise = 0.2f;

    /// <summary>
    /// A storey's shell standing on its origin, centred across: a floor slab with walls round it up to the storey's height, open at
    /// the top, where the storey above or the roof closes it.
    /// </summary>
    public static SolidMesh CreateTray(Vector3 size, float wall, float floor)
    {
        Vector3 half = size / 2;
        Vector3 inner = new(half.X - wall, 0, half.Z - wall);
        Vector3[] points =
        [
            new(-half.X, 0, -half.Z), new(half.X, 0, -half.Z), new(half.X, 0, half.Z), new(-half.X, 0, half.Z),
            new(-half.X, size.Y, -half.Z), new(half.X, size.Y, -half.Z), new(half.X, size.Y, half.Z), new(-half.X, size.Y, half.Z),
            new(-inner.X, floor, -inner.Z), new(inner.X, floor, -inner.Z), new(inner.X, floor, inner.Z), new(-inner.X, floor, inner.Z),
            new(-inner.X, size.Y, -inner.Z), new(inner.X, size.Y, -inner.Z), new(inner.X, size.Y, inner.Z), new(-inner.X, size.Y, inner.Z),
        ];

        // Seen from outside each face runs clockwise, as the box's faces do.
        int[][] faces =
        [
            [0, 1, 2, 3],
            [0, 4, 5, 1],
            [1, 5, 6, 2],
            [2, 6, 7, 3],
            [3, 7, 4, 0],
            [4, 12, 13, 5],
            [5, 13, 14, 6],
            [6, 14, 15, 7],
            [7, 15, 12, 4],
            [8, 9, 13, 12],
            [9, 10, 14, 13],
            [10, 11, 15, 14],
            [11, 8, 12, 15],
            [8, 11, 10, 9],
        ];
        return new SolidMesh(points, faces);
    }

    /// <summary>
    /// A flight of stairs standing on its origin and climbing toward +Z: <paramref name="height"/> high over <paramref name="run"/>
    /// along, in steps no higher than <see cref="StepRise"/>, solid underneath, a column to each step.
    /// </summary>
    public static SolidMesh CreateStairs(float width, float height, float run)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(height / StepRise));
        float rise = height / steps;
        float tread = run / steps;
        List<Vector3> points = [];
        List<int[]> faces = [];

        // Each side is a column per step, so every face stays convex. At each boundary between columns a side has a point on the
        // ground, one at the top of the step before and one at the top of the step after.
        int[][] ground = [new int[steps + 1], new int[steps + 1]];
        int[][] low = [new int[steps + 1], new int[steps + 1]];
        int[][] high = [new int[steps + 1], new int[steps + 1]];
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? width / 2 : -width / 2;
            for (int boundary = 0; boundary <= steps; boundary++)
            {
                float z = boundary * tread;
                ground[side][boundary] = Add(points, new Vector3(x, 0, z));
                low[side][boundary] = ground[side][boundary];
                if (boundary > 0)
                {
                    low[side][boundary] = Add(points, new Vector3(x, boundary * rise, z));
                }

                if (boundary < steps)
                {
                    high[side][boundary] = Add(points, new Vector3(x, (boundary + 1) * rise, z));
                }
            }
        }

        for (int step = 0; step < steps; step++)
        {
            List<int> column = [ground[0][step]];
            if (step > 0)
            {
                column.Add(low[0][step]);
            }

            column.AddRange([high[0][step], low[0][step + 1], ground[0][step + 1]]);
            faces.Add([.. column]);
            List<int> other = [ground[1][step + 1], low[1][step + 1], high[1][step]];
            if (step > 0)
            {
                other.Add(low[1][step]);
            }

            other.Add(ground[1][step]);
            faces.Add([.. other]);
            faces.Add([high[0][step], low[0][step], low[1][step], high[1][step]]);
            faces.Add([low[0][step + 1], high[0][step], high[1][step], low[1][step + 1]]);
            faces.Add([ground[0][step], ground[0][step + 1], ground[1][step + 1], ground[1][step]]);
        }

        faces.Add([ground[0][steps], low[0][steps], low[1][steps], ground[1][steps]]);
        return new SolidMesh(points, faces);
    }

    private static int Add(List<Vector3> points, Vector3 point)
    {
        points.Add(point);
        return points.Count - 1;
    }

    /// <summary>A slab standing on its origin, centred across: a roof, or an elevator's platform.</summary>
    public static SolidMesh CreateSlab(float width, float thickness, float depth)
    {
        return SolidMesh.Box(new Vector3(width, thickness, depth));
    }

    internal static SolidPart CreatePart(SolidMesh mesh, Vector3 position)
    {
        Transform place = new() { Position = position };
        return new SolidPart { Place = place, Mesh = mesh };
    }
}
