using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>
/// Moves shots along their rails for the director and poses them: plays timed shots towards a key, moves tracked shots with their
/// target, sends the markers of keys passed, and gives a shot's lens and world pose, turned to its target when it looks at it.
/// </summary>
internal sealed class ShotDriver
{
    /// <summary>How close in metres a tracked shot comes to its target's point before it stops easing and settles there.</summary>
    private const float SnapDistance = 1e-3f;

    private static readonly QueryDescription Targets = new QueryDescription().WithAll<CameraTarget, Transform>();
    private static readonly QueryDescription Named = new QueryDescription().WithAll<EntityName, Transform>();

    private readonly World _world;
    private readonly EventBus _events;
    private readonly Dictionary<Entity, ShotPlayback> _playbacks = [];

    public ShotDriver(World world, EventBus events)
    {
        _world = world;
        _events = events;
    }

    public void Play(Entity shot, int toKey, bool layers)
    {
        ShotPlayback playback = GetPlayback(shot);
        ShotTimeline timeline = GetTimeline(shot);
        float target = timeline.Duration;
        if (toKey >= 0 && toKey < timeline.Keys.Count)
        {
            target = timeline.Keys[toKey].Time;
        }

        playback.Target = target;
        playback.Playing = true;
        playback.LayersOff = !layers;
    }

    /// <summary>Whether the shot's layers move it: not while it plays a request that turned them off.</summary>
    public bool LayersEnabled(Entity shot)
    {
        return !_playbacks.TryGetValue(shot, out ShotPlayback? playback) || !playback.LayersOff;
    }

    public float GetTime(Entity shot)
    {
        if (_playbacks.TryGetValue(shot, out ShotPlayback? playback))
        {
            return playback.Time;
        }

        return 0;
    }

    public bool IsPlaying(Entity shot)
    {
        return _playbacks.TryGetValue(shot, out ShotPlayback? playback) && playback.Playing;
    }

    /// <summary>Makes a tracked shot jump to its target's point on its next drive instead of easing there, as when it goes live.</summary>
    public void Snap(Entity shot)
    {
        ShotPlayback playback = GetPlayback(shot);
        playback.Snap = true;
    }

    public void Forget(Entity shot)
    {
        _playbacks.Remove(shot);
    }

    /// <summary>Jumps a playing shot to where it plays to, sending the markers it passes and finishing it.</summary>
    public void Skip(Entity shot)
    {
        if (!_playbacks.TryGetValue(shot, out ShotPlayback? playback) || !playback.Playing)
        {
            return;
        }

        float from = playback.Time;
        playback.Time = playback.Target;
        playback.Playing = false;
        playback.LayersOff = false;
        SendMarkers(shot, from, playback.Time);
        _events.Send(new ShotFinishedEvent(shot));
    }

    /// <summary>Where each named shot the driver has moved is on its timeline, for a save.</summary>
    public List<SavedShot> Capture()
    {
        List<SavedShot> saved = [];
        foreach ((Entity shot, ShotPlayback playback) in _playbacks)
        {
            if (!_world.IsAlive(shot) || !_world.TryGet(shot, out EntityName name))
            {
                continue;
            }

            saved.Add(new SavedShot
            {
                Shot = name.Value,
                Time = playback.Time,
                Target = playback.Target,
                Playing = playback.Playing,
                LayersOff = playback.LayersOff,
            });
        }

        return saved;
    }

    /// <summary>Puts a shot back where a save had it; a tracked shot jumps to its target again.</summary>
    public void Restore(Entity shot, SavedShot saved)
    {
        ShotPlayback playback = GetPlayback(shot);
        playback.Time = saved.Time;
        playback.Target = saved.Target;
        playback.Playing = saved.Playing;
        playback.LayersOff = saved.LayersOff;
        playback.Snap = true;
    }

    /// <summary>Forgets every shot, as a load does before restoring them.</summary>
    public void Clear()
    {
        _playbacks.Clear();
    }

    /// <summary>Moves the shot for the frame by its drive, sending the markers of the keys it passes when asked.</summary>
    public void Drive(Entity shot, float seconds, bool sendMarkers)
    {
        ShotPlayback playback = GetPlayback(shot);
        float previousTime = playback.Time;
        Shot data = _world.Get<Shot>(shot);
        if (data.Drive == ShotDrive.Tracked)
        {
            Track(shot, data, playback, seconds);
        }
        else if (playback.Playing)
        {
            Advance(shot, playback, seconds);
        }

        if (sendMarkers)
        {
            SendMarkers(shot, previousTime, playback.Time);
        }
    }

    /// <summary>The shot's lens and world pose where it is now.</summary>
    public RenderCamera Pose(Entity shot)
    {
        ShotPlayback playback = GetPlayback(shot);
        ShotTimeline timeline = GetTimeline(shot);
        Shot data = _world.Get<Shot>(shot);
        ShotPose local = timeline.Evaluate(playback.Time);
        if (data.Drive == ShotDrive.Tracked)
        {
            local = timeline.EvaluateAtDistance(playback.Distance);
        }

        Transform transform = _world.Get<Transform>(shot);
        Camera lens = GetLens(shot);
        RenderCamera view = local.ToWorld(transform, lens);
        if (data.LookAtTarget && TryFindTarget(data, out Vector3 aim))
        {
            Transform pose = view.Transform;
            pose.Rotation = ShotPose.LookAt(pose.Position, aim, pose.Rotation);
            view = new RenderCamera(view.Camera, pose);
        }

        return view;
    }

    private void Advance(Entity shot, ShotPlayback playback, float seconds)
    {
        float toTarget = playback.Target - playback.Time;
        if (MathF.Abs(toTarget) <= seconds)
        {
            playback.Time = playback.Target;
            playback.Playing = false;
            playback.LayersOff = false;
            _events.Send(new ShotFinishedEvent(shot));
            return;
        }

        playback.Time += MathF.Sign(toTarget) * seconds;
    }

    /// <summary>Moves a tracked shot towards its target's nearest point on the rail, smoothed, and sets its time from there.</summary>
    private void Track(Entity shot, in Shot data, ShotPlayback playback, float seconds)
    {
        if (!TryFindTarget(data, out Vector3 aim))
        {
            return;
        }

        ShotTimeline timeline = GetTimeline(shot);
        Transform transform = _world.Get<Transform>(shot);
        Matrix4x4 shotWorld = transform.ToMatrix();
        Matrix4x4.Invert(shotWorld, out Matrix4x4 toShot);
        Vector3 local = Vector3.Transform(aim, toShot);
        float nearest = timeline.Rail.FindNearest(local);
        float distance = nearest;
        if (!playback.Snap && data.Smoothing > 0)
        {
            float exponent = -3 * seconds / data.Smoothing;
            float follow = 1 - MathF.Exp(exponent);
            distance = float.Lerp(playback.Distance, nearest, follow);
            if (MathF.Abs(nearest - distance) < SnapDistance)
            {
                distance = nearest;
            }
        }

        playback.Snap = false;
        playback.Distance = distance;
        playback.Time = timeline.GetTimeAtDistance(distance);
    }

    /// <summary>Sends the markers of the keys whose times the shot passed, going either way.</summary>
    private void SendMarkers(Entity shot, float from, float to)
    {
        if (from == to)
        {
            return;
        }

        ShotTimeline timeline = GetTimeline(shot);
        IReadOnlyList<ShotKey> keys = timeline.Keys;
        for (int index = 0; index < keys.Count; index++)
        {
            int key = index;
            if (to < from)
            {
                key = keys.Count - 1 - index;
            }

            float time = keys[key].Time;
            bool forward = from < time && time <= to;
            bool back = to <= time && time < from;
            if ((forward || back) && keys[key].Marker.IsValid)
            {
                _events.Send(new ShotMarkerEvent(shot, keys[key].Marker, key));
            }
        }
    }

    /// <summary>Where the shot's target is aimed at: the named entity, else the first camera target, at its aim height.</summary>
    public bool TryFindTarget(in Shot shot, out Vector3 aim)
    {
        Entity target = Entity.Null;
        string name = shot.Target;
        if (string.IsNullOrEmpty(name))
        {
            _world.Query(in Targets, (Entity entity) =>
            {
                if (target == Entity.Null || entity.Id < target.Id)
                {
                    target = entity;
                }
            });
        }
        else
        {
            _world.Query(in Named, (Entity entity, ref EntityName entityName) =>
            {
                if (target == Entity.Null && entityName.Value == name)
                {
                    target = entity;
                }
            });
        }

        aim = Vector3.Zero;
        if (target == Entity.Null)
        {
            return false;
        }

        aim = _world.Get<Transform>(target).Position;
        if (_world.TryGet(target, out CameraTarget cameraTarget))
        {
            aim.Y += cameraTarget.Height;
        }

        return true;
    }

    private Camera GetLens(Entity shot)
    {
        if (_world.TryGet(shot, out Camera camera))
        {
            return camera;
        }

        return new Camera();
    }

    private ShotPlayback GetPlayback(Entity shot)
    {
        if (!_playbacks.TryGetValue(shot, out ShotPlayback? playback))
        {
            playback = new ShotPlayback();
            _playbacks.Add(shot, playback);
        }

        return playback;
    }

    /// <summary>The shot's timeline, built again only when its keys or lens changed.</summary>
    private ShotTimeline GetTimeline(Entity shot)
    {
        ShotPlayback playback = GetPlayback(shot);
        List<ShotKey> keys = _world.Get<Shot>(shot).Keys;
        Camera lens = GetLens(shot);
        float zoom = lens.OrthographicHeight;
        if (lens.Projection == CameraProjection.Perspective)
        {
            zoom = lens.FieldOfView;
        }

        if (playback.Timeline is null || !ReferenceEquals(playback.Keys, keys) || playback.KeyCount != keys.Count || playback.DefaultZoom != zoom)
        {
            playback.Timeline = new ShotTimeline(keys, zoom);
            playback.Keys = keys;
            playback.KeyCount = keys.Count;
            playback.DefaultZoom = zoom;
        }

        return playback.Timeline;
    }

    /// <summary>What the driver keeps of a shot while the game runs.</summary>
    private sealed class ShotPlayback
    {
        public float Time { get; set; }

        public float Target { get; set; }

        public bool Playing { get; set; }

        /// <summary>Whether the play request running turned the shot's layers off.</summary>
        public bool LayersOff { get; set; }

        /// <summary>How far along its rail a tracked shot is.</summary>
        public float Distance { get; set; }

        /// <summary>Whether a tracked shot jumps to its target's point instead of easing there.</summary>
        public bool Snap { get; set; }

        public ShotTimeline? Timeline { get; set; }

        public List<ShotKey>? Keys { get; set; }

        public int KeyCount { get; set; }

        public float DefaultZoom { get; set; }
    }
}
