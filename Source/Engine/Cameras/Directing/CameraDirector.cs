using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>
/// Directs the camera each frame, a frame system and service: keeps one shot live (the scene's starting shot until told otherwise),
/// switches shots with a transition, keeps a stack of shots to return to, plays timed shots to their keys, moves tracked shots
/// with their target, sends the markers of keys passed, turns shots that look at their target and writes the live shot's
/// posed lens and world pose into its <see cref="ActiveCamera"/>, which the renderer draws from.
/// </summary>
public sealed class CameraDirector : BaseSystem<World, GameTime>
{
    /// <summary>How close in metres a tracked shot comes to its target's point before it stops easing and settles there.</summary>
    private const float SnapDistance = 1e-3f;

    private static readonly QueryDescription Shots = new QueryDescription().WithAll<Shot, Transform>();
    private static readonly QueryDescription Targets = new QueryDescription().WithAll<CameraTarget, Transform>();
    private static readonly QueryDescription Named = new QueryDescription().WithAll<EntityName, Transform>();

    private readonly EventBus _events;
    private readonly Dictionary<Entity, ShotPlayback> _playbacks = [];
    private readonly List<Entity> _stack = [];

    public CameraDirector(World world, EventBus events)
        : base(world)
    {
        _events = events;
        ArrayRegistry.Add<ActiveCamera>();
    }

    /// <summary>The shot drawn now, or <see cref="Entity.Null"/> before the first frame or without shots.</summary>
    public Entity Live { get; private set; } = Entity.Null;

    /// <summary>The shots <see cref="Return"/> goes back to, the latest last.</summary>
    public IReadOnlyList<Entity> Stack => _stack;

    /// <summary>Makes another shot live.</summary>
    public void Switch(Entity shot, ShotTransition transition)
    {
        if (!IsShot(shot))
        {
            return;
        }

        GoLive(shot);
    }

    /// <summary>Makes another shot live, keeping the live one to <see cref="Return"/> to, as a cutscene does.</summary>
    public void Push(Entity shot, ShotTransition transition)
    {
        if (!IsShot(shot))
        {
            return;
        }

        if (Live != Entity.Null)
        {
            _stack.Add(Live);
        }

        Switch(shot, transition);
    }

    /// <summary>Goes back to the shot live before the last <see cref="Push"/>; does nothing with none pushed.</summary>
    public void Return(ShotTransition transition)
    {
        while (_stack.Count > 0)
        {
            Entity shot = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            if (IsShot(shot))
            {
                Switch(shot, transition);
                return;
            }
        }
    }

    /// <summary>Plays a shot's timeline from where it is to a key's time (forwards or back), or to the end without one.</summary>
    public void Play(Entity shot, int toKey = -1)
    {
        if (!IsShot(shot))
        {
            return;
        }

        ShotPlayback playback = GetPlayback(shot);
        ShotTimeline timeline = GetTimeline(shot);
        float target = timeline.Duration;
        if (toKey >= 0 && toKey < timeline.Keys.Count)
        {
            target = timeline.Keys[toKey].Time;
        }

        playback.Target = target;
        playback.Playing = true;
    }

    /// <summary>Where a shot is on its timeline, in seconds.</summary>
    public float GetTime(Entity shot)
    {
        if (_playbacks.TryGetValue(shot, out ShotPlayback? playback))
        {
            return playback.Time;
        }

        return 0;
    }

    /// <summary>Whether a shot is playing towards a key.</summary>
    public bool IsPlaying(Entity shot)
    {
        return _playbacks.TryGetValue(shot, out ShotPlayback? playback) && playback.Playing;
    }

    public override void Update(in GameTime t)
    {
        if (!IsShot(Live))
        {
            _playbacks.Remove(Live);
            Entity start = FindStart();
            if (start == Entity.Null)
            {
                return;
            }

            GoLive(start);
        }

        float seconds = (float)t.Elapsed.TotalSeconds;
        Entity live = Live;
        ShotPlayback playback = GetPlayback(live);
        float previousTime = playback.Time;
        Shot shot = World.Get<Shot>(live);
        if (shot.Drive == ShotDrive.Tracked)
        {
            Track(live, shot, playback, seconds);
        }
        else if (playback.Playing)
        {
            Advance(live, playback, seconds);
        }

        SendMarkers(live, previousTime, playback.Time);
        Pose(live, playback);
    }

    /// <summary>Moves a tracked shot towards its target's nearest point on the rail, smoothed, and sets its time from there.</summary>
    private void Track(Entity live, in Shot shot, ShotPlayback playback, float seconds)
    {
        if (!TryFindTarget(shot, out Vector3 aim))
        {
            return;
        }

        ShotTimeline timeline = GetTimeline(live);
        Transform transform = World.Get<Transform>(live);
        Matrix4x4 shotWorld = transform.ToMatrix();
        Matrix4x4.Invert(shotWorld, out Matrix4x4 toShot);
        Vector3 local = Vector3.Transform(aim, toShot);
        float nearest = timeline.Rail.FindNearest(local);
        float distance = nearest;
        if (!playback.Snap && shot.Smoothing > 0)
        {
            float exponent = -3 * seconds / shot.Smoothing;
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
    private void SendMarkers(Entity live, float from, float to)
    {
        if (from == to)
        {
            return;
        }

        ShotTimeline timeline = GetTimeline(live);
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
                _events.Send(new ShotMarkerEvent(live, keys[key].Marker, key));
            }
        }
    }

    /// <summary>Where the shot's target is aimed at: the named entity, else the first camera target, at its aim height.</summary>
    private bool TryFindTarget(in Shot shot, out Vector3 aim)
    {
        Entity target = Entity.Null;
        string name = shot.Target;
        if (string.IsNullOrEmpty(name))
        {
            World.Query(in Targets, (Entity entity) =>
            {
                if (target == Entity.Null || entity.Id < target.Id)
                {
                    target = entity;
                }
            });
        }
        else
        {
            World.Query(in Named, (Entity entity, ref EntityName entityName) =>
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

        aim = World.Get<Transform>(target).Position;
        if (World.TryGet(target, out CameraTarget cameraTarget))
        {
            aim.Y += cameraTarget.Height;
        }

        return true;
    }

    private void Advance(Entity shot, ShotPlayback playback, float seconds)
    {
        float toTarget = playback.Target - playback.Time;
        if (MathF.Abs(toTarget) <= seconds)
        {
            playback.Time = playback.Target;
            playback.Playing = false;
            _events.Send(new ShotFinishedEvent(shot));
            return;
        }

        playback.Time += MathF.Sign(toTarget) * seconds;
    }

    /// <summary>Writes the shot's pose at its time, in world space, into its active camera.</summary>
    private void Pose(Entity shot, ShotPlayback playback)
    {
        ShotTimeline timeline = GetTimeline(shot);
        Shot data = World.Get<Shot>(shot);
        ShotPose local = timeline.Evaluate(playback.Time);
        if (data.Drive == ShotDrive.Tracked)
        {
            local = timeline.EvaluateAtDistance(playback.Distance);
        }

        Transform transform = World.Get<Transform>(shot);
        Matrix4x4 shotWorld = transform.ToMatrix();
        Vector3 position = Vector3.Transform(local.Position, shotWorld);
        Quaternion rotation = Quaternion.Concatenate(local.Rotation, transform.Rotation);
        if (data.LookAtTarget && TryFindTarget(data, out Vector3 aim))
        {
            rotation = LookAt(position, aim, rotation);
        }

        Camera lens = GetLens(shot);
        if (lens.Projection == CameraProjection.Perspective)
        {
            lens.FieldOfView = local.Zoom;
        }
        else
        {
            lens.OrthographicHeight = local.Zoom;
        }

        Transform pose = new() { Position = position, Rotation = rotation };
        ActiveCamera active = new() { Lens = lens, Pose = pose };
        if (World.Has<ActiveCamera>(shot))
        {
            World.Set(shot, active);
        }
        else
        {
            World.Add(shot, active);
        }
    }

    /// <summary>The rotation looking from a point at another with +Y up, or the fallback when they meet or the look is straight up or down.</summary>
    private static Quaternion LookAt(Vector3 from, Vector3 to, Quaternion fallback)
    {
        Vector3 toTarget = to - from;
        if (toTarget.LengthSquared() < 1e-8f)
        {
            return fallback;
        }

        Vector3 forward = Vector3.Normalize(toTarget);
        float upright = MathF.Abs(Vector3.Dot(forward, Vector3.UnitY));
        if (upright > 0.9999f)
        {
            return fallback;
        }

        Matrix4x4 world = Matrix4x4.CreateWorld(Vector3.Zero, forward, Vector3.UnitY);
        return Quaternion.CreateFromRotationMatrix(world);
    }

    private void GoLive(Entity shot)
    {
        Entity previous = Live;
        if (previous == shot)
        {
            return;
        }

        if (World.IsAlive(previous) && World.Has<ActiveCamera>(previous))
        {
            World.Remove<ActiveCamera>(previous);
        }

        if (!World.IsAlive(previous))
        {
            previous = Entity.Null;
        }

        Live = shot;
        ShotPlayback playback = GetPlayback(shot);
        playback.Snap = true;
        Pose(shot, playback);
        _events.Send(new ShotChangedEvent(previous, shot));
    }

    /// <summary>The earliest created shot marked to start, else the earliest created shot.</summary>
    private Entity FindStart()
    {
        Entity first = Entity.Null;
        Entity start = Entity.Null;
        World.Query(in Shots, (Entity entity, ref Shot shot) =>
        {
            if (first == Entity.Null || entity.Id < first.Id)
            {
                first = entity;
            }

            if (shot.Start && (start == Entity.Null || entity.Id < start.Id))
            {
                start = entity;
            }
        });

        if (start != Entity.Null)
        {
            return start;
        }

        return first;
    }

    private bool IsShot(Entity shot)
    {
        return shot != Entity.Null && World.IsAlive(shot) && World.Has<Shot>(shot) && World.Has<Transform>(shot);
    }

    private Camera GetLens(Entity shot)
    {
        if (World.TryGet(shot, out Camera camera))
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
        List<ShotKey> keys = World.Get<Shot>(shot).Keys;
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

    /// <summary>What the director keeps of a shot while the game runs.</summary>
    private sealed class ShotPlayback
    {
        public float Time { get; set; }

        public float Target { get; set; }

        public bool Playing { get; set; }

        /// <summary>How far along its rail a tracked shot is.</summary>
        public float Distance { get; set; }

        /// <summary>Whether a tracked shot jumps to its target's point instead of easing there, as when it goes live.</summary>
        public bool Snap { get; set; }

        public ShotTimeline? Timeline { get; set; }

        public List<ShotKey>? Keys { get; set; }

        public int KeyCount { get; set; }

        public float DefaultZoom { get; set; }
    }
}
