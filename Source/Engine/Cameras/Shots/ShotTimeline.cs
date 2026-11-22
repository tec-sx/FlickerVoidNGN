using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>
/// A shot's keys as a rail and a timeline in the shot's space: between two keys the camera moves along the rail by arc length, turns
/// with slerp and zooms linearly, all eased as the later key says. Without keys it is a single key at the shot's origin.
/// </summary>
public sealed class ShotTimeline
{
    private readonly ShotKey[] _keys;
    private readonly float[] _zooms;

    /// <param name="keys">The keys, in order of time.</param>
    /// <param name="defaultZoom">The camera's own zoom, for keys with none.</param>
    public ShotTimeline(IReadOnlyList<ShotKey> keys, float defaultZoom)
    {
        _keys = [.. keys];
        if (_keys.Length == 0)
        {
            _keys = [new ShotKey()];
        }

        _zooms = new float[_keys.Length];
        Vector3[] points = new Vector3[_keys.Length];
        for (int index = 0; index < _keys.Length; index++)
        {
            ShotKey key = _keys[index];
            points[index] = key.Position;
            _zooms[index] = defaultZoom;
            if (key.Zoom > 0)
            {
                _zooms[index] = key.Zoom;
            }
        }

        Rail = new Rail(points);
    }

    public Rail Rail { get; }

    public IReadOnlyList<ShotKey> Keys => _keys;

    /// <summary>The time of the last key.</summary>
    public float Duration => _keys[^1].Time;

    /// <summary>The pose at a time of the timeline, clamped to its first and last keys.</summary>
    public ShotPose Evaluate(float time)
    {
        int segment = FindSegmentAtTime(time);
        if (segment < 0)
        {
            return PoseAt(0, 0);
        }

        float start = _keys[segment].Time;
        float span = _keys[segment + 1].Time - start;
        float fraction = 1;
        if (span > 0)
        {
            fraction = (time - start) / span;
        }

        ShotEase ease = _keys[segment + 1].Ease;
        float eased = ease.Apply(fraction);
        return PoseAt(segment, eased);
    }

    /// <summary>The pose at a distance along the rail, turned and zoomed in proportion to the distance between its keys.</summary>
    public ShotPose EvaluateAtDistance(float distance)
    {
        int segment = FindSegmentAtDistance(distance, out float fraction);
        return PoseAt(segment, fraction);
    }

    /// <summary>The time of the timeline at a distance along the rail, in proportion to the distance between its keys.</summary>
    public float GetTimeAtDistance(float distance)
    {
        int segment = FindSegmentAtDistance(distance, out float fraction);
        if (_keys.Length == 1)
        {
            return _keys[0].Time;
        }

        return float.Lerp(_keys[segment].Time, _keys[segment + 1].Time, fraction);
    }

    /// <summary>The segment holding a distance along the rail, and how far through it the distance is.</summary>
    private int FindSegmentAtDistance(float distance, out float fraction)
    {
        fraction = 0;
        if (_keys.Length == 1)
        {
            return 0;
        }

        int segment = 0;
        while (segment < _keys.Length - 2 && distance > Rail.GetPointDistance(segment + 1))
        {
            segment++;
        }

        float start = Rail.GetPointDistance(segment);
        float span = Rail.GetPointDistance(segment + 1) - start;
        fraction = 1;
        if (span > 0)
        {
            float along = (distance - start) / span;
            fraction = Math.Clamp(along, 0, 1);
        }

        return segment;
    }

    /// <summary>The segment from a key to the next that holds the time, or -1 when the timeline has one key or the time is before it starts.</summary>
    private int FindSegmentAtTime(float time)
    {
        if (_keys.Length == 1 || time <= _keys[0].Time)
        {
            return -1;
        }

        for (int segment = 0; segment < _keys.Length - 2; segment++)
        {
            if (time < _keys[segment + 1].Time)
            {
                return segment;
            }
        }

        return _keys.Length - 2;
    }

    private ShotPose PoseAt(int segment, float fraction)
    {
        if (_keys.Length == 1)
        {
            return new ShotPose(_keys[0].Position, _keys[0].Rotation, _zooms[0]);
        }

        float fromDistance = Rail.GetPointDistance(segment);
        float toDistance = Rail.GetPointDistance(segment + 1);
        float distance = float.Lerp(fromDistance, toDistance, fraction);
        Vector3 position = Rail.GetPoint(distance);
        Quaternion rotation = Quaternion.Slerp(_keys[segment].Rotation, _keys[segment + 1].Rotation, fraction);
        float zoom = float.Lerp(_zooms[segment], _zooms[segment + 1], fraction);
        return new ShotPose(position, rotation, zoom);
    }
}
