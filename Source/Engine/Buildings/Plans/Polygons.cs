using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// Flat shapes across a floor and the solids standing on them. A point's X is the building's X and its Y the building's Z. A polygon
/// "round to the left" has its inside on the left of someone walking its edges seen from above, as a solid's top face runs; its
/// signed area (<see cref="SignedArea"/>) is then below zero.
/// </summary>
public static class Polygons
{
    private const float Tiny = 1e-5f;

    /// <summary>Twice the polygon's area, below zero when it runs round to the left.</summary>
    public static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        float sum = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            Vector2 current = polygon[index];
            Vector2 next = polygon[(index + 1) % polygon.Count];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }

        return sum;
    }

    public static float Area(IReadOnlyList<Vector2> polygon) => MathF.Abs(SignedArea(polygon)) / 2;

    /// <summary>The polygon running round to the left, turned round when it ran the other way.</summary>
    public static List<Vector2> ToLeft(IReadOnlyList<Vector2> polygon)
    {
        List<Vector2> points = [.. polygon];
        if (SignedArea(points) > 0)
        {
            points.Reverse();
        }

        return points;
    }

    /// <summary>The unit vector to the left of a direction, seen from above.</summary>
    public static Vector2 Left(Vector2 direction)
    {
        Vector2 unit = Vector2.Normalize(direction);
        return new Vector2(unit.Y, -unit.X);
    }

    public static float Cross(Vector2 first, Vector2 second) => (first.X * second.Y) - (first.Y * second.X);

    /// <summary>Whether the point is inside the polygon (an even-odd count of crossings).</summary>
    public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
        {
            Vector2 a = polygon[index];
            Vector2 b = polygon[previous];
            if ((a.Y > point.Y) != (b.Y > point.Y))
            {
                float x = a.X + ((point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                if (point.X < x)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    /// <summary>A point well inside the polygon: its centre when that is inside, else the middle of the widest span across its middle.</summary>
    public static Vector2 InteriorPoint(IReadOnlyList<Vector2> polygon)
    {
        Vector2 centre = Vector2.Zero;
        foreach (Vector2 point in polygon)
        {
            centre += point;
        }

        centre /= polygon.Count;
        if (Contains(polygon, centre))
        {
            return centre;
        }

        float low = polygon.Min(static point => point.Y);
        float high = polygon.Max(static point => point.Y);
        Vector2 best = centre;
        float widest = -1;
        for (int row = 1; row < 8; row++)
        {
            float y = low + ((high - low) * row / 8);
            List<float> crossings = [];
            for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
            {
                Vector2 a = polygon[index];
                Vector2 b = polygon[previous];
                if ((a.Y > y) != (b.Y > y))
                {
                    crossings.Add(a.X + ((y - a.Y) * (b.X - a.X) / (b.Y - a.Y)));
                }
            }

            crossings.Sort();
            for (int pair = 0; pair + 1 < crossings.Count; pair += 2)
            {
                float width = crossings[pair + 1] - crossings[pair];
                if (width > widest)
                {
                    widest = width;
                    best = new Vector2((crossings[pair] + crossings[pair + 1]) / 2, y);
                }
            }
        }

        return best;
    }

    /// <summary>The polygon without points that lie on the line between their neighbours, or on top of one.</summary>
    public static List<Vector2> Simplify(IReadOnlyList<Vector2> polygon)
    {
        List<Vector2> points = [.. polygon];
        bool removed = true;
        while (removed && points.Count > 3)
        {
            removed = false;
            for (int index = 0; index < points.Count && points.Count > 3; index++)
            {
                Vector2 previous = points[(index + points.Count - 1) % points.Count];
                Vector2 current = points[index];
                Vector2 next = points[(index + 1) % points.Count];
                Vector2 before = current - previous;
                Vector2 after = next - current;
                bool same = before.LengthSquared() < Tiny * Tiny;
                bool straight = MathF.Abs(Cross(before, after)) < Tiny * MathF.Max(1, before.Length() * after.Length()) && Vector2.Dot(before, after) >= 0;
                if (same || straight)
                {
                    points.RemoveAt(index);
                    removed = true;
                    index--;
                }
            }
        }

        return points;
    }

    /// <summary>Cuts a simple polygon into triangles by clipping ears; each triangle runs round the same way as the polygon.</summary>
    public static List<(int A, int B, int C)> Triangulate(IReadOnlyList<Vector2> polygon)
    {
        List<(int, int, int)> triangles = [];
        List<int> left = [.. Enumerable.Range(0, polygon.Count)];
        float sign = MathF.Sign(SignedArea(polygon));
        int guard = polygon.Count * polygon.Count;
        while (left.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int index = 0; index < left.Count; index++)
            {
                int a = left[(index + left.Count - 1) % left.Count];
                int b = left[index];
                int c = left[(index + 1) % left.Count];
                if (!IsEar(polygon, left, a, b, c, sign))
                {
                    continue;
                }

                triangles.Add((a, b, c));
                left.RemoveAt(index);
                clipped = true;
                break;
            }

            if (!clipped)
            {
                // A polygon that crosses itself has no ear left: the rest is fanned, as a solid's faces are.
                break;
            }
        }

        for (int index = 1; index + 1 < left.Count; index++)
        {
            triangles.Add((left[0], left[index], left[index + 1]));
        }

        return triangles;
    }

    private static bool IsEar(IReadOnlyList<Vector2> polygon, List<int> left, int a, int b, int c, float sign)
    {
        Vector2 pa = polygon[a];
        Vector2 pb = polygon[b];
        Vector2 pc = polygon[c];
        if (Cross(pb - pa, pc - pb) * sign <= Tiny)
        {
            return false;
        }

        foreach (int other in left)
        {
            if (other == a || other == b || other == c)
            {
                continue;
            }

            if (InTriangle(polygon[other], pa, pb, pc, sign))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c, float sign)
    {
        float first = Cross(b - a, point - a) * sign;
        float second = Cross(c - b, point - b) * sign;
        float third = Cross(a - c, point - c) * sign;
        return first >= -Tiny && second >= -Tiny && third >= -Tiny;
    }

    /// <summary>
    /// A solid standing on the polygon from <paramref name="bottom"/> to <paramref name="top"/>, its top and bottom cut into triangles
    /// when the polygon is not convex.
    /// </summary>
    public static SolidMesh Prism(IReadOnlyList<Vector2> polygon, float bottom, float top)
    {
        List<Vector2> shape = ToLeft(Simplify(polygon));
        int count = shape.Count;
        List<Vector3> points = new(count * 2);
        foreach (Vector2 point in shape)
        {
            points.Add(new Vector3(point.X, bottom, point.Y));
        }

        foreach (Vector2 point in shape)
        {
            points.Add(new Vector3(point.X, top, point.Y));
        }

        List<int[]> faces = [];
        for (int index = 0; index < count; index++)
        {
            int next = (index + 1) % count;
            faces.Add([index, next, count + next, count + index]);
        }

        if (IsConvex(shape))
        {
            faces.Add([.. Enumerable.Range(count, count)]);
            faces.Add([.. Enumerable.Range(0, count).Reverse()]);
        }
        else
        {
            foreach ((int a, int b, int c) in Triangulate(shape))
            {
                faces.Add([count + a, count + b, count + c]);
                faces.Add([c, b, a]);
            }
        }

        return new SolidMesh(points, faces);
    }

    /// <summary>Whether every corner of the polygon turns the same way.</summary>
    public static bool IsConvex(IReadOnlyList<Vector2> polygon)
    {
        float sign = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            Vector2 a = polygon[index];
            Vector2 b = polygon[(index + 1) % polygon.Count];
            Vector2 c = polygon[(index + 2) % polygon.Count];
            float turn = Cross(b - a, c - b);
            if (MathF.Abs(turn) < Tiny)
            {
                continue;
            }

            if (sign == 0)
            {
                sign = MathF.Sign(turn);
            }
            else if (MathF.Sign(turn) != sign)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Moves each edge of a polygon running round to the left outward (to its right) by its own distance, and joins the moved edges
    /// where they cross; a negative distance moves an edge in.
    /// </summary>
    public static List<Vector2> Offset(IReadOnlyList<Vector2> polygon, IReadOnlyList<float> distances)
    {
        int count = polygon.Count;
        List<Vector2> result = new(count);
        for (int index = 0; index < count; index++)
        {
            int previous = (index + count - 1) % count;
            Vector2 before = polygon[index] - polygon[previous];
            Vector2 after = polygon[(index + 1) % count] - polygon[index];
            Vector2 beforeOut = -Left(before) * distances[previous];
            Vector2 afterOut = -Left(after) * distances[index];
            Vector2? met = Meet(polygon[previous] + beforeOut, before, polygon[index] + afterOut, after);
            result.Add(met ?? (polygon[index] + afterOut));
        }

        return result;
    }

    /// <summary>Where two lines, each through a point along a direction, cross; null when they run side by side.</summary>
    public static Vector2? Meet(Vector2 first, Vector2 firstDirection, Vector2 second, Vector2 secondDirection)
    {
        float denominator = Cross(firstDirection, secondDirection);
        if (MathF.Abs(denominator) < 1e-6f * firstDirection.Length() * secondDirection.Length())
        {
            return null;
        }

        float along = Cross(second - first, secondDirection) / denominator;
        return first + (firstDirection * along);
    }

    /// <summary>The part of a convex or upright polygon between two values of X, cut along both lines.</summary>
    public static List<Vector2> ClipAcross(IReadOnlyList<Vector2> polygon, float low, float high)
    {
        List<Vector2> above = ClipSide(polygon, low, 1);
        return ClipSide(above, high, -1);
    }

    // Keeps the points whose X lies on the kept side of the line (at or past it going the way of the sign), with the crossings.
    private static List<Vector2> ClipSide(IReadOnlyList<Vector2> polygon, float line, float sign)
    {
        const float Tiny = 1e-6f;
        List<Vector2> kept = [];
        for (int index = 0; index < polygon.Count; index++)
        {
            Vector2 current = polygon[index];
            Vector2 next = polygon[(index + 1) % polygon.Count];
            float here = (current.X - line) * sign;
            float there = (next.X - line) * sign;
            if (here >= -Tiny)
            {
                kept.Add(current);
            }

            if ((here > Tiny && there < -Tiny) || (here < -Tiny && there > Tiny))
            {
                float share = here / (here - there);
                kept.Add(Vector2.Lerp(current, next, share));
            }
        }

        return kept;
    }
}
