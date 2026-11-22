using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>
/// Directs the camera each frame, a frame system and service: keeps one shot live (the scene's starting shot until told otherwise),
/// switches shots with a transition (cut, cross-fade, fade or blend), keeps a stack of shots to return to, drives the live shot
/// along its rail, moves it by its camera layers (shakes and zoom punches asked for by commands), and writes its posed lens and world pose, with the transition laid over the frame, into its
/// <see cref="ActiveCamera"/>, which the renderer draws from.
/// </summary>
public sealed class CameraDirector : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Shots = new QueryDescription().WithAll<Shot, Transform>();

    private readonly EventBus _events;
    private readonly ShotDriver _driver;
    private readonly CameraLayerRunner _layers;
    private readonly List<Entity> _stack = [];

    private float _fade;
    private float _fadeTarget;
    private float _fadeSpeed;
    private Vector3 _fadeColor;
    private Entity _pending = Entity.Null;
    private float _fadeInAfter = -1;

    private Entity _dissolveFrom = Entity.Null;
    private float _dissolve;
    private float _dissolveSpeed;

    private RenderCamera _blendFrom;
    private float _blend = 1;
    private float _blendSpeed;
    private ShotEase _blendEase;

    public CameraDirector(Game game)
        : base(game.World)
    {
        _events = game.Events;
        _driver = new ShotDriver(game.World, game.Events);
        ILogger<CameraDirector> logger = game.LoggerFactory.CreateLogger<CameraDirector>();
        _layers = new CameraLayerRunner(game.Services, logger);
        ArrayRegistry.Add<ActiveCamera>();
        _events.Handle((in CameraShakeCommand command) => _layers.Shakes.Add(command));
        _events.Handle((in ZoomPunchCommand command) => _layers.Punches.Add(command));
    }

    /// <summary>
    /// Where the pointer is, from the centre of the screen, -1 to 1 with +Y up, for look layers; the game sets it each frame, and
    /// null means unknown.
    /// </summary>
    public Vector2? Pointer { get; set; }

    /// <summary>The shot drawn now, or <see cref="Entity.Null"/> before the first frame or without shots.</summary>
    public Entity Live { get; private set; } = Entity.Null;

    /// <summary>The shots <see cref="Return"/> goes back to, the latest last.</summary>
    public IReadOnlyList<Entity> Stack => _stack;

    /// <summary>Whether the world is fading, or a fade waits to cut to its shot.</summary>
    public bool IsFading => _fade != _fadeTarget || _pending != Entity.Null;

    /// <summary>Whether a cross-fade, blend or fade is still running.</summary>
    public bool IsTransitioning => IsFading || _dissolve > 0 || _blend < 1;

    /// <summary>Makes another shot live with the transition; a later request replaces one still waiting.</summary>
    public void Switch(Entity shot, ShotTransition transition)
    {
        if (!IsShot(shot))
        {
            return;
        }

        _pending = Entity.Null;
        switch (transition.Kind)
        {
            case TransitionKind.Fade:
                StartFade(transition);
                _pending = shot;
                if (_fade >= 1)
                {
                    FinishFadeOut();
                }

                break;
            case TransitionKind.CrossFade:
                StartCrossFade(transition.Duration);
                GoLive(shot);
                break;
            case TransitionKind.Blend:
                StartBlend(transition);
                GoLive(shot);
                break;
            default:
                EndMoves();
                GoLive(shot);
                break;
        }
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

    /// <summary>Fades the world to a colour over the duration and holds it there until <see cref="FadeIn"/>.</summary>
    public void FadeOut(float duration, Vector3 color)
    {
        _pending = Entity.Null;
        _fadeColor = color;
        _fadeInAfter = -1;
        SetFadeTarget(1, duration);
    }

    /// <summary>Fades the world back in from its colour over the duration.</summary>
    public void FadeIn(float duration)
    {
        _pending = Entity.Null;
        _fadeInAfter = -1;
        SetFadeTarget(0, duration);
    }

    /// <summary>
    /// Plays a shot's timeline from where it is to a key's time (forwards or back), or to the end without one; a cutscene can turn
    /// the shot's layers off until it gets there.
    /// </summary>
    public void Play(Entity shot, int toKey = -1, bool layers = true)
    {
        if (IsShot(shot))
        {
            _driver.Play(shot, toKey, layers);
        }
    }

    /// <summary>Where a shot is on its timeline, in seconds.</summary>
    public float GetTime(Entity shot) => _driver.GetTime(shot);

    /// <summary>Whether a shot is playing towards a key.</summary>
    public bool IsPlaying(Entity shot) => _driver.IsPlaying(shot);

    public override void Update(in GameTime t)
    {
        if (!IsShot(Live))
        {
            _driver.Forget(Live);
            _layers.Forget(Live);
            Entity start = FindStart();
            if (start == Entity.Null)
            {
                return;
            }

            GoLive(start);
        }

        float seconds = (float)t.Elapsed.TotalSeconds;
        UpdateFade(seconds);
        _driver.Drive(Live, seconds, sendMarkers: true);
        UpdateDissolve(seconds);
        _blend = MathF.Min(_blend + (_blendSpeed * seconds), 1);
        WriteView(seconds);
        _layers.EndFrame();
    }

    private void StartFade(in ShotTransition transition)
    {
        _fadeColor = transition.Color;
        _fadeInAfter = MathF.Max(transition.InDuration, 0);
        SetFadeTarget(1, transition.Duration);
    }

    private void SetFadeTarget(float target, float duration)
    {
        _fadeTarget = target;
        _fadeSpeed = float.PositiveInfinity;
        if (duration > 0)
        {
            _fadeSpeed = 1 / duration;
        }

        if (float.IsPositiveInfinity(_fadeSpeed))
        {
            _fade = target;
        }
    }

    private void UpdateFade(float seconds)
    {
        float toTarget = _fadeTarget - _fade;
        float step = _fadeSpeed * seconds;
        if (toTarget != 0 && MathF.Abs(toTarget) <= step)
        {
            _fade = _fadeTarget;
        }
        else if (toTarget != 0)
        {
            _fade += MathF.Sign(toTarget) * step;
        }

        if (_fade >= 1 && _pending != Entity.Null)
        {
            FinishFadeOut();
        }
    }

    /// <summary>Cuts to the shot the fade waited for while the world is dark, then starts fading back in.</summary>
    private void FinishFadeOut()
    {
        Entity shot = _pending;
        _pending = Entity.Null;
        EndMoves();
        if (IsShot(shot))
        {
            GoLive(shot);
        }

        if (_fadeInAfter >= 0)
        {
            SetFadeTarget(0, _fadeInAfter);
            _fadeInAfter = -1;
        }
    }

    private void StartCrossFade(float duration)
    {
        EndMoves();
        if (duration <= 0 || !IsShot(Live))
        {
            return;
        }

        _dissolveFrom = Live;
        _dissolve = 1;
        _dissolveSpeed = 1 / duration;
    }

    private void UpdateDissolve(float seconds)
    {
        if (_dissolve <= 0)
        {
            return;
        }

        if (!IsShot(_dissolveFrom))
        {
            _dissolve = 0;
            return;
        }

        _driver.Drive(_dissolveFrom, seconds, sendMarkers: false);
        _dissolve = MathF.Max(_dissolve - (_dissolveSpeed * seconds), 0);
    }

    /// <summary>Starts moving from the view drawn now, which may itself be blending, to the next shot.</summary>
    private void StartBlend(in ShotTransition transition)
    {
        RenderCamera? drawn = null;
        if (IsShot(Live) && World.TryGet(Live, out ActiveCamera active))
        {
            drawn = new RenderCamera(active.Lens, active.Pose);
        }

        EndMoves();
        if (transition.Duration <= 0 || drawn is null)
        {
            return;
        }

        _blendFrom = drawn.Value;
        _blend = 0;
        _blendSpeed = 1 / transition.Duration;
        _blendEase = transition.Ease;
    }

    /// <summary>Ends a cross-fade or blend, as a cut does.</summary>
    private void EndMoves()
    {
        _dissolve = 0;
        _dissolveFrom = Entity.Null;
        _blend = 1;
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
        _driver.Snap(shot);
        WriteView(0);
        _events.Send(new ShotChangedEvent(previous, shot));
    }

    /// <summary>Writes the live shot's view, moved by its layers and blended when a blend runs, and the transition over it into its active camera.</summary>
    private void WriteView(float seconds)
    {
        RenderCamera view = _driver.Pose(Live);
        if (_driver.LayersEnabled(Live))
        {
            Shot shot = World.Get<Shot>(Live);
            Vector3? target = null;
            if (_driver.TryFindTarget(shot, out Vector3 aim))
            {
                target = aim;
            }

            view = _layers.Apply(Live, shot.Rig, view, target, Pointer, seconds);
        }

        if (_blend < 1)
        {
            float eased = _blendEase.Apply(_blend);
            view = Mix(_blendFrom, view, eased);
        }

        ActiveCamera active = new() { Lens = view.Camera, Pose = view.Transform, Fade = _fade, FadeColor = _fadeColor };
        if (_dissolve > 0)
        {
            RenderCamera from = _driver.Pose(_dissolveFrom);
            active.Dissolve = _dissolve;
            active.DissolveLens = from.Camera;
            active.DissolvePose = from.Transform;
        }

        if (World.Has<ActiveCamera>(Live))
        {
            World.Set(Live, active);
        }
        else
        {
            World.Add(Live, active);
        }
    }

    /// <summary>The view part of the way from one to another: moved, turned and, between lenses of one projection, zoomed.</summary>
    private static RenderCamera Mix(in RenderCamera from, in RenderCamera to, float amount)
    {
        Vector3 position = Vector3.Lerp(from.Transform.Position, to.Transform.Position, amount);
        Quaternion rotation = Quaternion.Slerp(from.Transform.Rotation, to.Transform.Rotation, amount);
        Camera lens = to.Camera;
        if (from.Camera.Projection == lens.Projection)
        {
            lens.OrthographicHeight = float.Lerp(from.Camera.OrthographicHeight, lens.OrthographicHeight, amount);
            lens.FieldOfView = float.Lerp(from.Camera.FieldOfView, lens.FieldOfView, amount);
        }

        Transform pose = new() { Position = position, Rotation = rotation };
        return new RenderCamera(lens, pose);
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
}
