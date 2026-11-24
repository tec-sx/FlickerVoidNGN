using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Ai.Senses;

/// <summary>
/// Lets perceivers see and hear (a fixed system and service): a <see cref="Perceivable"/> within the sight radius and angle with nothing
/// in between is seen (<see cref="TargetSeenEvent"/>) until it passes the lose-sight radius, leaves the view, is hidden or is gone
/// (<see cref="TargetLostEvent"/>); a <see cref="NoiseEvent"/> within hearing is heard (<see cref="NoiseHeardEvent"/>) and remembered
/// for the perceiver's memory (<see cref="NoiseForgottenEvent"/>). Bodies other than the perceiver's own hide what is behind them when the game has physics.
/// Each perceiver looks once every sight interval, and they take turns, so their rays spread over the fixed steps.
/// </summary>
public sealed class PerceptionSystem : BaseSystem<World, GameTime>
{
    /// <summary>Metres short of a target where a hit no longer hides it, so a body under the target's own point doesn't.</summary>
    private const float Clearance = 0.05f;

    /// <summary>The share of a sight interval each new perceiver's first look comes after the one before it: the golden ratio's, which spreads any number of them evenly.</summary>
    private const float TurnShare = 0.618034f;

    private static readonly QueryDescription Perceivers = new QueryDescription().WithAll<Perception, Perceived, Transform>();
    private static readonly QueryDescription Targets = new QueryDescription().WithAll<Perceivable, Transform>();

    private readonly EventBus _events;
    private readonly IPhysicsSystem? _physics;
    private readonly List<(Entity Entity, Vector3 Point)> _targets = [];
    private float _turn;

    public PerceptionSystem(World world, EventBus events, EntityLifecycle lifecycle, IPhysicsSystem? physics)
        : base(world)
    {
        _events = events;
        _physics = physics;
        ArrayRegistry.Add<Perceived>();
        lifecycle.OnActivate((Entity entity, ref Perception _) => Activate(entity));
        lifecycle.OnDeactivate((Entity entity, ref Perception _) => Deactivate(entity));
        events.Handle((in NoiseEvent noise) => Hear(noise));
    }

    /// <summary>The targets the perceiver sees, in the order it saw them.</summary>
    public IReadOnlyList<Entity> GetSeen(Entity perceiver)
    {
        if (World.IsAlive(perceiver) && World.TryGet(perceiver, out Perceived perceived))
        {
            return perceived.Seen;
        }

        return [];
    }

    /// <summary>The last noise the perceiver heard from each instigator it still remembers.</summary>
    public IReadOnlyList<HeardNoise> GetHeard(Entity perceiver)
    {
        if (World.IsAlive(perceiver) && World.TryGet(perceiver, out Perceived perceived))
        {
            return perceived.Heard;
        }

        return [];
    }

    public override void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        _targets.Clear();
        World.Query(in Targets, (Entity entity, ref Perceivable perceivable, ref Transform transform) =>
        {
            Vector3 point = transform.Position + (Vector3.UnitY * perceivable.Height);
            _targets.Add((entity, point));
        });

        World.Query(in Perceivers, (Entity entity, ref Perception perception, ref Perceived perceived, ref Transform transform) =>
        {
            perceived.UntilLook -= seconds;
            if (perceived.UntilLook <= 0)
            {
                perceived.UntilLook = MathF.Max(perceived.UntilLook + perception.SightInterval, 0);
                Look(entity, perception, perceived, transform);
            }

            Remember(entity, perception, perceived, seconds);
        });
    }

    private void Look(Entity perceiver, in Perception perception, in Perceived perceived, in Transform transform)
    {
        Vector3 eye = transform.Position + (Vector3.UnitY * perception.EyeHeight);
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, transform.Rotation);
        float minCosine = MathF.Cos(float.DegreesToRadians(perception.SightAngle));
        List<Entity> seen = perceived.Seen;
        for (int index = seen.Count - 1; index >= 0; index--)
        {
            Entity target = seen[index];
            if (!TryFindTarget(target, out Vector3 point) || !CanSee(perceiver, eye, forward, minCosine, perception.LoseSightRadius, target, point))
            {
                seen.RemoveAt(index);
                _events.Send(new TargetLostEvent(perceiver, target));
            }
        }

        foreach ((Entity target, Vector3 point) in _targets)
        {
            if (target == perceiver || seen.Contains(target))
            {
                continue;
            }

            if (CanSee(perceiver, eye, forward, minCosine, perception.SightRadius, target, point))
            {
                seen.Add(target);
                _events.Send(new TargetSeenEvent(perceiver, target));
            }
        }
    }

    private bool CanSee(Entity perceiver, Vector3 eye, Vector3 forward, float minCosine, float radius, Entity target, Vector3 point)
    {
        Vector3 toTarget = point - eye;
        float distance = toTarget.Length();
        if (distance > radius)
        {
            return false;
        }

        if (distance < 1e-4f)
        {
            return true;
        }

        Vector3 direction = toTarget / distance;
        if (Vector3.Dot(forward, direction) < minCosine)
        {
            return false;
        }

        if (_physics is null)
        {
            return true;
        }

        Ray ray = new(eye, direction);
        if (!_physics.RayCast(ray, distance, perceiver, out RayHit hit))
        {
            return true;
        }

        return hit.Entity == target || hit.Distance >= distance - Clearance;
    }

    private bool TryFindTarget(Entity target, out Vector3 point)
    {
        foreach ((Entity entity, Vector3 targetPoint) in _targets)
        {
            if (entity == target)
            {
                point = targetPoint;
                return true;
            }
        }

        point = default;
        return false;
    }

    private void Remember(Entity perceiver, in Perception perception, in Perceived perceived, float seconds)
    {
        List<HeardNoise> heard = perceived.Heard;
        for (int index = heard.Count - 1; index >= 0; index--)
        {
            HeardNoise noise = heard[index];
            float age = noise.Age + seconds;
            if (age > perception.Memory)
            {
                heard.RemoveAt(index);
                _events.Send(new NoiseForgottenEvent(perceiver, noise.Instigator));
            }
            else
            {
                heard[index] = noise with { Age = age };
            }
        }
    }

    private void Hear(in NoiseEvent noise)
    {
        NoiseEvent made = noise;
        World.Query(in Perceivers, (Entity entity, ref Perception perception, ref Perceived perceived, ref Transform transform) =>
        {
            if (entity == made.Instigator)
            {
                return;
            }

            float range = perception.HearingRange * made.Loudness;
            if (made.Range > 0)
            {
                range = MathF.Min(range, made.Range);
            }

            float distance = Vector3.Distance(transform.Position, made.Point);
            if (distance > range)
            {
                return;
            }

            List<HeardNoise> heard = perceived.Heard;
            HeardNoise fresh = new(made.Instigator, made.Point, 0);
            int index = heard.FindIndex(known => known.Instigator == made.Instigator);
            if (index >= 0)
            {
                heard[index] = fresh;
            }
            else
            {
                heard.Add(fresh);
            }

            _events.Send(new NoiseHeardEvent(entity, made.Instigator, made.Point));
        });
    }

    private void Activate(Entity entity)
    {
        if (!World.Has<Perceived>(entity))
        {
            Perception perception = World.Get<Perception>(entity);
            World.Add(entity, new Perceived { Seen = [], Heard = [], UntilLook = perception.SightInterval * _turn });
            _turn = (_turn + TurnShare) % 1;
        }
    }

    private void Deactivate(Entity entity)
    {
        if (World.Has<Perceived>(entity))
        {
            World.Remove<Perceived>(entity);
        }
    }
}
