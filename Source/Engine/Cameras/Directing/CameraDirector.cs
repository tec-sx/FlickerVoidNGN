using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Gameplay.Tags;
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
/// along its rail, moves it by its camera layers (shakes and zoom punches asked for by commands), and writes its posed lens and world pose, with the transition laid over the frame
/// and its depth fade (the main camera's, changed by the shot's override, measured from its target, blended as shots change), into its
/// <see cref="ActiveCamera"/>, which the renderer draws from.
/// </summary>
public sealed class CameraDirector : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Shots = new QueryDescription().WithAll<Shot, Transform>();
    private static readonly QueryDescription NamedShots = new QueryDescription().WithAll<Shot, Transform, EntityName>();

    private readonly EventBus _events;
    private readonly ShotDriver _driver;
    private readonly CameraLayerRunner _layers;
    private readonly List<Entity> _stack = [];
    private readonly List<CameraWait> _waits = [];
    private readonly List<CameraWait> _done = [];

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
    private DepthFade _blendFromFade;
    private Vector3? _blendFromFocus;
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
        _events.Handle((in ShotFinishedEvent finished) => EndWaits(CameraWaitKind.ShotFinished, finished.Shot, default));
        _events.Handle((in ShotMarkerEvent marker) => EndWaits(CameraWaitKind.Marker, marker.Shot, marker.Marker));
        game.SaveStates.AddGlobal<SavedCameras>("cameras", Save, Restore);
    }

    /// <summary>
    /// Where the pointer is, from the centre of the screen, -1 to 1 with +Y up, for look layers; the game sets it each frame, and
    /// null means unknown.
    /// </summary>
    public Vector2? Pointer { get; set; }

    /// <summary>What the cameras follow now: the live shot's target, else the camera target spawned first; <see cref="Entity.Null"/> without one.</summary>
    public Entity FindTarget()
    {
        Shot shot = new();
        if (IsShot(Live))
        {
            shot = World.Get<Shot>(Live);
        }

        return _driver.FindTarget(shot);
    }

    /// <summary>The shot drawn now, or <see cref="Entity.Null"/> before the first frame or without shots.</summary>
    public Entity Live { get; private set; } = Entity.Null;

    /// <summary>
    /// The seconds the switch to the live shot moves over on screen: a cross-fade's or blend's duration, and 0 after a cut, a fade
    /// (whose shot goes live while the world is dark), the scene's start or a load.
    /// </summary>
    public float SwitchSeconds { get; private set; }

    /// <summary>The shots <see cref="Return"/> goes back to, the latest last.</summary>
    public IReadOnlyList<Entity> Stack => _stack;

    /// <summary>Whether the world is fading, or a fade waits to cut to its shot.</summary>
    public bool IsFading => _fade != _fadeTarget || _pending != Entity.Null;

    /// <summary>Whether a cross-fade, blend or fade is still running.</summary>
    public bool IsTransitioning => IsFading || _dissolve > 0 || _blend < 1;

    /// <summary>Whether the player may skip the live shot: it is skippable and playing.</summary>
    public bool CanSkip => IsShot(Live) && World.Get<Shot>(Live).Skippable && _driver.IsPlaying(Live);

    /// <summary>The shot entity with the name, or <see cref="Entity.Null"/> when there is none or the name is empty.</summary>
    public Entity FindShot(string name)
    {
        Entity found = Entity.Null;
        if (string.IsNullOrEmpty(name))
        {
            return found;
        }

        World.Query(in NamedShots, (Entity entity, ref EntityName entityName) =>
        {
            if (found == Entity.Null && entityName.Value == name)
            {
                found = entity;
            }
        });
        return found;
    }

    /// <summary>The shot entity with the name, or the live shot when the name is empty.</summary>
    public Entity FindShotOrLive(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return Live;
        }

        return FindShot(name);
    }

    /// <summary>Switches, pushes or returns, as the mode says; a return needs no shot.</summary>
    public void Request(ShotSwitchMode mode, Entity shot, ShotTransition transition)
    {
        switch (mode)
        {
            case ShotSwitchMode.Push:
                Push(shot, transition);
                break;
            case ShotSwitchMode.Return:
                Return(transition);
                break;
            default:
                Switch(shot, transition);
                break;
        }
    }

    /// <summary>Shakes the live camera at a strength from 0 to 1, fading over the seconds given, as a <see cref="CameraShakeCommand"/> does.</summary>
    public void Shake(float strength, float duration)
    {
        CameraShakeCommand shake = new(strength, duration);
        _layers.Shakes.Add(shake);
    }

    /// <summary>Skips the live shot when it may be skipped, jumping it to where it plays to.</summary>
    public void Skip()
    {
        if (CanSkip)
        {
            _driver.Skip(Live);
        }
    }

    /// <summary>Calls back once no transition runs, at once when none does.</summary>
    public void WhenTransitionEnds(object owner, Action done)
    {
        if (!IsTransitioning)
        {
            done();
            return;
        }

        _waits.Add(new CameraWait(owner, CameraWaitKind.Transition, Entity.Null, default, done));
    }

    /// <summary>Calls back once the shot finishes playing, at once when it doesn't play.</summary>
    public void WhenShotFinishes(object owner, Entity shot, Action done)
    {
        if (!_driver.IsPlaying(shot))
        {
            done();
            return;
        }

        _waits.Add(new CameraWait(owner, CameraWaitKind.ShotFinished, shot, default, done));
    }

    /// <summary>Calls back when the shot, or any shot for <see cref="Entity.Null"/>, reaches a key with the marker.</summary>
    public void WhenMarker(object owner, Entity shot, Tag marker, Action done)
    {
        _waits.Add(new CameraWait(owner, CameraWaitKind.Marker, shot, marker, done));
    }

    /// <summary>Stops the owner's waits without calling them back.</summary>
    public void CancelWaits(object owner)
    {
        _waits.RemoveAll(wait => wait.Owner.Equals(owner));
    }

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
                GoLive(shot, transition.Duration);
                break;
            case TransitionKind.Blend:
                StartBlend(transition);
                GoLive(shot, transition.Duration);
                break;
            default:
                EndMoves();
                GoLive(shot);
                break;
        }
    }

    /// <summary>Makes another shot live, keeping the live one to <see cref="Return"/> to, as a cutscene does; pushing the live shot keeps nothing.</summary>
    public void Push(Entity shot, ShotTransition transition)
    {
        if (!IsShot(shot))
        {
            return;
        }

        if (Live != Entity.Null && Live != shot)
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

        float seconds = 0;
        if (!t.Paused)
        {
            seconds = (float)t.Elapsed.TotalSeconds;
        }

        UpdateFade(seconds);
        _driver.Drive(Live, seconds, sendMarkers: true);
        UpdateDissolve(seconds);
        _blend = MathF.Min(_blend + (_blendSpeed * seconds), 1);
        WriteView(seconds);
        _layers.EndFrame();
        if (!IsTransitioning)
        {
            EndWaits(CameraWaitKind.Transition, Entity.Null, default);
        }
    }

    /// <summary>Calls back, after taking them off, the waits of the kind for the shot (any shot when theirs is none) and marker.</summary>
    private void EndWaits(CameraWaitKind kind, Entity shot, Tag marker)
    {
        _done.Clear();
        foreach (CameraWait wait in _waits)
        {
            bool sameShot = wait.Shot == Entity.Null || wait.Shot == shot;
            bool sameMarker = kind != CameraWaitKind.Marker || wait.Marker.Equals(marker);
            if (wait.Kind == kind && sameShot && sameMarker)
            {
                _done.Add(wait);
            }
        }

        List<CameraWait> done = [.. _done];
        foreach (CameraWait wait in done)
        {
            _waits.Remove(wait);
        }

        foreach (CameraWait wait in done)
        {
            wait.Done();
        }
    }

    private SavedCameras? Save()
    {
        string live = string.Empty;
        if (IsShot(Live) && World.TryGet(Live, out EntityName liveName))
        {
            live = liveName.Value;
        }

        List<string> stack = [];
        foreach (Entity shot in _stack)
        {
            if (World.IsAlive(shot) && World.TryGet(shot, out EntityName name))
            {
                stack.Add(name.Value);
            }
        }

        List<SavedShot> shots = _driver.Capture();
        return new SavedCameras { Live = live, Stack = stack, Shots = shots, Fade = _fade, FadeColor = _fadeColor };
    }

    /// <summary>Puts the director back where a save had it: no transition runs, the fade holds where it was and the live shot is cut to.</summary>
    private void Restore(SavedCameras saved)
    {
        _pending = Entity.Null;
        EndMoves();
        _fade = saved.Fade;
        _fadeTarget = saved.Fade;
        _fadeColor = saved.FadeColor;
        _fadeInAfter = -1;
        _driver.Clear();
        foreach (SavedShot savedShot in saved.Shots)
        {
            Entity shot = FindShot(savedShot.Shot);
            if (shot != Entity.Null)
            {
                _driver.Restore(shot, savedShot);
            }
        }

        _stack.Clear();
        foreach (string name in saved.Stack)
        {
            Entity shot = FindShot(name);
            if (shot != Entity.Null)
            {
                _stack.Add(shot);
            }
        }

        Entity live = FindShot(saved.Live);
        if (live != Entity.Null)
        {
            GoLive(live);
            WriteView(0);
        }
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
            _blendFromFade = active.DepthFade;
            _blendFromFocus = active.Focus;
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

    private void GoLive(Entity shot, float seconds = 0)
    {
        Entity previous = Live;
        if (previous == shot)
        {
            return;
        }

        SwitchSeconds = MathF.Max(seconds, 0);

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
        Shot shot = World.Get<Shot>(Live);
        Vector3? target = null;
        if (_driver.TryFindTarget(shot, out Vector3 aim))
        {
            target = aim;
        }

        if (_driver.LayersEnabled(Live))
        {
            view = _layers.Apply(Live, shot.Rig, view, target, Pointer, seconds);
        }

        DepthFade fade = MainCamera.FindFor(World, Live);
        Vector3? focus = target;
        if (_blend < 1)
        {
            float eased = _blendEase.Apply(_blend);
            view = Mix(_blendFrom, view, eased);
            fade = DepthFade.Lerp(_blendFromFade, fade, eased);
            focus = MixFocus(_blendFromFocus, focus, eased);
        }

        ActiveCamera active = new() { Lens = view.Camera, Pose = view.Transform, Fade = _fade, FadeColor = _fadeColor };
        if (_dissolve > 0)
        {
            RenderCamera from = _driver.Pose(_dissolveFrom);
            active.Dissolve = _dissolve;
            active.DissolveLens = from.Camera;
            active.DissolvePose = from.Transform;
            DepthFade fromFade = MainCamera.FindFor(World, _dissolveFrom);
            fade = DepthFade.Lerp(fade, fromFade, _dissolve);
        }

        active.DepthFade = fade;
        active.Focus = focus;

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

    /// <summary>The focus part of the way from one to another; a missing one gives way to the other.</summary>
    private static Vector3? MixFocus(Vector3? from, Vector3? to, float amount)
    {
        if (from is { } start && to is { } end)
        {
            return Vector3.Lerp(start, end, amount);
        }

        return to ?? from;
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
