using System.Numerics;

namespace FlickerVoid.Engine.Runtime.Geometry;

/// <summary>
/// A centripetal Catmull-Rom spline through points, which passes through every point and neither loops nor overshoots much at sharp
/// turns, measured by arc length so positions along it move at an even speed; camera shots ride them.
/// </summary>
public sealed class Rail
{
    /// <summary>How many straight pieces measure each segment between two points.</summary>
    public const int SamplesPerSegment = 32;

    private const float MinKnotInterval = 1e-4f;

    private readonly Vector3[] _points;
    private readonly Vector3[] _samples;
    private readonly float[] _distances;

    public Rail(IReadOnlyList<Vector3> points)
    {
        if (points.Count == 0)
        {
            throw new ArgumentException("A rail needs at least one point.", nameof(points));
        }

        _points = [.. points];
        int segments = _points.Length - 1;
        int count = (segments * SamplesPerSegment) + 1;
        _samples = new Vector3[count];
        _distances = new float[count];
        _samples[0] = _points[0];
        for (int index = 1; index < count; index++)
        {
            int segment = (index - 1) / SamplesPerSegment;
            float t = (float)(index - (segment * SamplesPerSegment)) / SamplesPerSegment;
            _samples[index] = Evaluate(segment, t);
            float step = Vector3.Distance(_samples[index - 1], _samples[index]);
            _distances[index] = _distances[index - 1] + step;
        }
    }

    public IReadOnlyList<Vector3> Points => _points;

    /// <summary>The rail's length from its first point to its last.</summary>
    public float Length => _distances[^1];

    /// <summary>How far along the rail a point lies.</summary>
    public float GetPointDistance(int index)
    {
        int sample = index * SamplesPerSegment;
        return _distances[sample];
    }

    /// <summary>The position a distance along the rail, clamped to its ends.</summary>
    public Vector3 GetPoint(float distance)
    {
        if (_points.Length == 1 || distance <= 0)
        {
            return _points[0];
        }

        if (distance >= Length)
        {
            return _points[^1];
        }

        int sample = FindSample(distance);
        float pieceLength = _distances[sample + 1] - _distances[sample];
        float fraction = 0;
        if (pieceLength > 0)
        {
            fraction = (distance - _distances[sample]) / pieceLength;
        }

        int segment = sample / SamplesPerSegment;
        float t = (sample - (segment * SamplesPerSegment) + fraction) / SamplesPerSegment;
        return Evaluate(segment, t);
    }

    /// <summary>How far along the rail its point nearest the given point lies.</summary>
    public float FindNearest(Vector3 point)
    {
        float nearest = 0;
        float nearestSquared = float.MaxValue;
        for (int sample = 0; sample < _samples.Length - 1; sample++)
        {
            Vector3 start = _samples[sample];
            Vector3 piece = _samples[sample + 1] - start;
            float pieceSquared = piece.LengthSquared();
            float along = 0;
            if (pieceSquared > 0)
            {
                Vector3 toPoint = point - start;
                float projected = Vector3.Dot(toPoint, piece) / pieceSquared;
                along = Math.Clamp(projected, 0, 1);
            }

            Vector3 closest = start + (piece * along);
            float apartSquared = Vector3.DistanceSquared(point, closest);
            if (apartSquared < nearestSquared)
            {
                nearestSquared = apartSquared;
                float pieceLength = _distances[sample + 1] - _distances[sample];
                nearest = _distances[sample] + (pieceLength * along);
            }
        }

        return nearest;
    }

    /// <summary>The last sample at or before the distance, by binary search of the arc length table.</summary>
    private int FindSample(float distance)
    {
        int low = 0;
        int high = _distances.Length - 2;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (_distances[middle] <= distance)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    /// <summary>The Barry and Goldman form of the centripetal spline between a point and the next, at a parameter from 0 to 1.</summary>
    private Vector3 Evaluate(int segment, float t)
    {
        Vector3 p1 = _points[segment];
        Vector3 p2 = _points[segment + 1];
        Vector3 p0 = (2 * p1) - p2;
        if (segment > 0)
        {
            p0 = _points[segment - 1];
        }

        Vector3 p3 = (2 * p2) - p1;
        if (segment + 2 < _points.Length)
        {
            p3 = _points[segment + 2];
        }

        float t0 = 0;
        float t1 = t0 + KnotInterval(p0, p1);
        float t2 = t1 + KnotInterval(p1, p2);
        float t3 = t2 + KnotInterval(p2, p3);
        float time = t1 + ((t2 - t1) * t);
        Vector3 a1 = Mix(p0, p1, t0, t1, time);
        Vector3 a2 = Mix(p1, p2, t1, t2, time);
        Vector3 a3 = Mix(p2, p3, t2, t3, time);
        Vector3 b1 = Mix(a1, a2, t0, t2, time);
        Vector3 b2 = Mix(a2, a3, t1, t3, time);
        return Mix(b1, b2, t1, t2, time);
    }

    private static float KnotInterval(Vector3 from, Vector3 to)
    {
        float distance = Vector3.Distance(from, to);
        return MathF.Max(MathF.Sqrt(distance), MinKnotInterval);
    }

    private static Vector3 Mix(Vector3 from, Vector3 to, float start, float end, float time)
    {
        float weight = (time - start) / (end - start);
        return Vector3.Lerp(from, to, weight);
    }
}
