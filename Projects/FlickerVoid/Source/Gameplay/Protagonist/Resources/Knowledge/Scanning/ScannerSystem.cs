using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Gameplay.Protagonist.Control;
using FlickerVoid.Gameplay.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

using World = Arch.Core.World;

/// <summary>
/// The protagonist's scanner, ported from the Unreal scanner for point and click: <see cref="ScanMode"/> turns scan mode on and off,
/// which reveals the scannables within range whose conditions pass; the revealed one the pointer is on, in sight of the scanner, is
/// focused, and holding <see cref="Scan"/> scans it over its definition's duration. The first completed scan applies its effects.
/// Saves keep which scannables were scanned. A fixed system and a game service.
/// </summary>
public sealed partial class ScannerSystem : BaseSystem<World, GameTime>
{
    public static readonly Tag ScanMode = new("Input.ScanMode");

    public static readonly Tag Scan = new("Input.Scan");

    private static readonly QueryDescription Scanners = new QueryDescription().WithAll<Scanner, ScannerState, Transform>();

    private static readonly QueryDescription Scannables = new QueryDescription().WithAll<ActiveScannable, Scannable, Transform>();

    private readonly Game _game;
    private readonly EventBus _events;
    private readonly IPhysicsSystem _physics;
    private readonly PointerPicker _picker;
    private readonly ILogger<ScannerSystem> _logger;

    public ScannerSystem(Game game, IPhysicsSystem physics, PointerPicker picker)
        : base(game.World)
    {
        _game = game;
        _events = game.Events;
        _physics = physics;
        _picker = picker;
        _logger = game.LoggerFactory.CreateLogger<ScannerSystem>();
        ArrayRegistry.Add<ScannerState>();
        ArrayRegistry.Add<ActiveScannable>();
        game.Lifecycle.OnActivate((Entity entity, ref Scanner _) => World.Add(entity, new ScannerState { Focus = Entity.Null, Revealed = [] }));
        game.Lifecycle.OnDeactivate((Entity entity, ref Scanner _) => Stop(entity));
        game.Lifecycle.OnActivate((Entity entity, ref Scannable scannable) => Activate(entity, scannable));
        game.Lifecycle.OnDeactivate((Entity entity, ref Scannable _) => Deactivate(entity));
        _events.Handle((in InputActionEvent action) => HandleInput(action));
        game.SaveStates.Add("scannable", FlickerVoidJsonContext.Default.SavedScannable, Save, Restore);
    }

    public bool IsActive(Entity scanner) => World.IsAlive(scanner) && World.TryGet(scanner, out ScannerState state) && state.Active;

    /// <summary>The scannable the scanner focuses, or <see cref="Entity.Null"/>.</summary>
    public Entity GetFocus(Entity scanner)
    {
        if (World.IsAlive(scanner) && World.TryGet(scanner, out ScannerState state))
        {
            return state.Focus;
        }

        return Entity.Null;
    }

    /// <summary>How far the scan of the focus has come, from 0 to 1.</summary>
    public float GetProgress(Entity scanner)
    {
        if (World.IsAlive(scanner) && World.TryGet(scanner, out ScannerState state))
        {
            return state.Progress;
        }

        return 0;
    }

    public bool IsRevealed(Entity target) => World.IsAlive(target) && World.TryGet(target, out ActiveScannable active) && active.Revealed;

    public bool WasScanned(Entity target) => World.IsAlive(target) && World.TryGet(target, out ActiveScannable active) && active.Scanned;

    public ScanDefinition? GetDefinition(Entity target)
    {
        if (World.IsAlive(target) && World.TryGet(target, out ActiveScannable active))
        {
            return active.Definition;
        }

        return null;
    }

    /// <summary>The target's scan entries whose conditions pass for the scanner.</summary>
    public IReadOnlyList<ScanEntry> GetVisibleEntries(Entity scanner, Entity target)
    {
        ScanDefinition? definition = GetDefinition(target);
        if (definition is null)
        {
            return [];
        }

        ConditionContext context = CreateContext(scanner, target);
        return definition.Entries.Where(entry => entry.VisibleWhen.Evaluate(context)).ToList();
    }

    public void SetScanMode(Entity scanner, bool active)
    {
        if (!World.IsAlive(scanner) || !World.Has<ScannerState>(scanner))
        {
            return;
        }

        ref ScannerState state = ref World.Get<ScannerState>(scanner);
        if (state.Active == active)
        {
            return;
        }

        state.Active = active;
        state.UpdateLeft = 0;
        if (!active)
        {
            StopScan(ref state);
            SetFocus(scanner, ref state, Entity.Null);
            Hide(ref state, []);
        }

        _events.Send(new ScanModeChangedEvent(scanner, active));
    }

    /// <summary>Begins scanning the focus, in scan mode.</summary>
    public void StartScan(Entity scanner)
    {
        if (World.IsAlive(scanner) && World.Has<ScannerState>(scanner))
        {
            ref ScannerState state = ref World.Get<ScannerState>(scanner);
            state.Scanning = state.Active;
        }
    }

    public void StopScan(Entity scanner)
    {
        if (World.IsAlive(scanner) && World.Has<ScannerState>(scanner))
        {
            ref ScannerState state = ref World.Get<ScannerState>(scanner);
            StopScan(ref state);
        }
    }

    public override void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        World.Query(in Scanners, (Entity entity, ref Scanner scanner, ref ScannerState state, ref Transform transform) =>
        {
            if (!state.Active)
            {
                return;
            }

            state.UpdateLeft -= seconds;
            if (state.UpdateLeft <= 0)
            {
                state.UpdateLeft = scanner.UpdateInterval;
                UpdateTargets(entity, scanner, ref state, transform.Position);
            }

            Progress(entity, ref state, seconds);
        });
    }

    private void Progress(Entity scanner, ref ScannerState state, float seconds)
    {
        Entity target = state.Focus;
        if (!state.Scanning || target == Entity.Null || !World.IsAlive(target) || !World.Has<ActiveScannable>(target))
        {
            return;
        }

        ref ActiveScannable active = ref World.Get<ActiveScannable>(target);
        if (active.Scanned)
        {
            return;
        }

        float duration = active.Definition.ScanDuration;
        if (duration > 0)
        {
            state.Progress = MathF.Min(state.Progress + (seconds / duration), 1);
        }
        else
        {
            state.Progress = 1;
        }

        if (state.Progress < 1)
        {
            return;
        }

        state.Progress = 0;
        active.Scanned = true;
        ScanDefinition definition = active.Definition;
        ConditionContext context = CreateContext(scanner, target);
        definition.OnScanned.Apply(context);
        _events.Send(new ScannedEvent(scanner, target));
    }

    private void UpdateTargets(Entity scanner, in Scanner settings, ref ScannerState state, Vector3 position)
    {
        List<Entity> revealed = [];
        float range = settings.Range;
        World.Query(in Scannables, (Entity entity, ref ActiveScannable active, ref Transform transform) =>
        {
            if (entity == scanner || Vector3.Distance(position, transform.Position) > range)
            {
                return;
            }

            ConditionContext context = CreateContext(scanner, entity);
            if (active.Definition.ScannableWhen.Evaluate(context))
            {
                revealed.Add(entity);
            }
        });

        Hide(ref state, revealed);
        foreach (Entity entity in revealed)
        {
            ref ActiveScannable active = ref World.Get<ActiveScannable>(entity);
            if (!active.Revealed)
            {
                active.Revealed = true;
                _events.Send(new ScanRevealChangedEvent(entity, true));
            }
        }

        state.Revealed = revealed;
        Entity focus = Entity.Null;
        if (_picker.TryPick(out RayHit hit) && revealed.Contains(hit.Entity) && CanSee(scanner, settings, position, hit.Entity))
        {
            focus = hit.Entity;
        }

        SetFocus(scanner, ref state, focus);
    }

    private bool CanSee(Entity scanner, in Scanner settings, Vector3 position, Entity target)
    {
        if (!settings.RequireLineOfSight)
        {
            return true;
        }

        Vector3 eye = position + (Vector3.UnitY * settings.EyeHeight);
        Vector3 targetPosition = World.Get<Transform>(target).Position;
        float height = World.Get<Scannable>(target).Height;
        Vector3 point = targetPosition + (Vector3.UnitY * height);
        Vector3 toPoint = point - eye;
        float distance = toPoint.Length();
        if (distance <= 0)
        {
            return true;
        }

        Ray ray = new(eye, toPoint / distance);
        if (!_physics.RayCast(ray, distance, scanner, out RayHit hit))
        {
            return true;
        }

        return hit.Entity == target;
    }

    private void SetFocus(Entity scanner, ref ScannerState state, Entity focus)
    {
        if (state.Focus == focus)
        {
            return;
        }

        state.Focus = focus;
        state.Progress = 0;
        _events.Send(new ScanFocusChangedEvent(scanner, focus));
    }

    /// <summary>Stops showing what the scanner revealed and no longer reveals.</summary>
    private void Hide(ref ScannerState state, List<Entity> kept)
    {
        foreach (Entity entity in state.Revealed)
        {
            if (kept.Contains(entity) || !World.IsAlive(entity) || !World.Has<ActiveScannable>(entity))
            {
                continue;
            }

            World.Get<ActiveScannable>(entity).Revealed = false;
            _events.Send(new ScanRevealChangedEvent(entity, false));
        }

        state.Revealed = kept;
    }

    private static void StopScan(ref ScannerState state)
    {
        state.Scanning = false;
        state.Progress = 0;
    }

    private void HandleInput(in InputActionEvent action)
    {
        if (action.Action != ScanMode && action.Action != Scan)
        {
            return;
        }

        List<Entity> scanners = [];
        World.Query(in Scanners, (Entity entity) =>
        {
            if (World.Has<Protagonist>(entity))
            {
                scanners.Add(entity);
            }
        });

        foreach (Entity scanner in scanners)
        {
            Use(scanner, action);
        }
    }

    private void Use(Entity scanner, in InputActionEvent action)
    {
        if (action.Action == ScanMode && action.Phase == InputPhase.Pressed)
        {
            bool active = IsActive(scanner);
            SetScanMode(scanner, !active);
        }
        else if (action.Action == Scan && action.Phase == InputPhase.Pressed)
        {
            StartScan(scanner);
        }
        else if (action.Action == Scan && action.Phase == InputPhase.Released)
        {
            StopScan(scanner);
        }
    }

    private ConditionContext CreateContext(Entity scanner, Entity target)
    {
        WorldEntityView scannerView = new(World, scanner);
        WorldEntityView targetView = new(World, target);
        return new ConditionContext(_game.Facts) { Instigator = scannerView, Target = targetView, Services = _game.Services };
    }

    private void Activate(Entity entity, Scannable scannable)
    {
        DataAssets? data = _game.Services.GetService<DataAssets>();
        if (data is null)
        {
            LogNoDataAssets(scannable.Definition);
            return;
        }

        ScanDefinition definition;
        try
        {
            definition = data.Load<ScanDefinition>(scannable.Definition);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogDefinitionFailed(scannable.Definition, exception.Message);
            return;
        }

        World.Add(entity, new ActiveScannable { Definition = definition });
    }

    private SavedScannable? Save(Entity entity)
    {
        if (!World.TryGet(entity, out ActiveScannable active))
        {
            return null;
        }

        return new SavedScannable { Scanned = active.Scanned };
    }

    private void Restore(Entity entity, SavedScannable saved)
    {
        if (World.Has<ActiveScannable>(entity))
        {
            World.Get<ActiveScannable>(entity).Scanned = saved.Scanned;
        }
    }

    private void Deactivate(Entity entity)
    {
        if (World.Has<ActiveScannable>(entity))
        {
            World.Remove<ActiveScannable>(entity);
        }
    }

    private void Stop(Entity entity)
    {
        SetScanMode(entity, false);
        if (World.Has<ScannerState>(entity))
        {
            World.Remove<ScannerState>(entity);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "There are no data assets to read the scan {Definition} from, so it stays hidden from scanners")]
    private partial void LogNoDataAssets(string definition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The scan {Definition} can't be read, so it stays hidden from scanners: {Reason}")]
    private partial void LogDefinitionFailed(string definition, string reason);
}
