using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Geometry;

using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Protagonist.Control;

using World = Arch.Core.World;

/// <summary>
/// Point and click: a press of <see cref="Use"/> walks the protagonist to where the mouse points, or, on an interactable, focuses it,
/// walks within reach and uses it.
/// </summary>
public sealed class ClickControlSystem
{
    public static readonly Tag Use = new("Input.Use");

    /// <summary>How far into its reach the protagonist walks before using something, so small misses still reach it.</summary>
    public const float ApproachFraction = 0.75f;

    private const float MaxClickDistance = 500;

    private static readonly QueryDescription Protagonists = new QueryDescription().WithAll<Protagonist, Interactor, CharacterMover, Transform>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();

    private readonly World _world;
    private readonly EventBus _events;
    private readonly IInputState _input;
    private readonly IGameWindow _window;
    private readonly IPhysicsSystem _physics;
    private Entity _pending = Entity.Null;

    public ClickControlSystem(World world, EventBus events, IInputState input, IGameWindow window, IPhysicsSystem physics)
    {
        _world = world;
        _events = events;
        _input = input;
        _window = window;
        _physics = physics;
        events.Handle((in InputActionEvent action) => HandleInput(action));
        events.Handle((in ArrivedEvent arrived) => HandleArrived(arrived.Character));
    }

    private void HandleInput(in InputActionEvent action)
    {
        if (action.Action != Use || action.Phase != InputPhase.Pressed)
        {
            return;
        }

        Entity protagonist = FindFirst(Protagonists);
        if (protagonist == Entity.Null || !TryPick(out RayHit hit))
        {
            return;
        }

        if (_world.Has<ActiveInteractable>(hit.Entity))
        {
            _events.Send(new FocusCommand(protagonist, hit.Entity));
            _pending = hit.Entity;
            Approach(protagonist, hit.Entity);
            return;
        }

        _pending = Entity.Null;
        _events.Send(new FocusCommand(protagonist, Entity.Null));
        _events.Send(new MoveCommand(protagonist, hit.Point));
    }

    private void HandleArrived(Entity character)
    {
        if (_pending == Entity.Null || !_world.Has<Protagonist>(character))
        {
            return;
        }

        _pending = Entity.Null;
        _events.Send(new InteractionInput(character, Use, InputPhase.Pressed));
    }

    /// <summary>Walks within reach of the target, or uses it at once when already there.</summary>
    private void Approach(Entity protagonist, Entity target)
    {
        Vector3 position = _world.Get<Transform>(protagonist).Position;
        Vector3 targetPosition = _world.Get<Transform>(target).Position;
        float reach = _world.Get<Interactor>(protagonist).Reach;
        Vector3 away = position - targetPosition;
        away.Y = 0;
        float distance = away.Length();
        float standOff = reach * ApproachFraction;
        if (distance <= standOff)
        {
            _events.Send(new StopCommand(protagonist));
            HandleArrived(protagonist);
            return;
        }

        Vector3 direction = away / distance;
        Vector3 stand = targetPosition + (direction * standOff);
        stand.Y = position.Y;
        _events.Send(new MoveCommand(protagonist, stand));
    }

    private bool TryPick(out RayHit hit)
    {
        hit = default;
        Entity camera = FindFirst(Cameras);
        Vector2D<int> size = _window.Size;
        if (camera == Entity.Null || size.X <= 0 || size.Y <= 0)
        {
            return false;
        }

        Camera lens = _world.Get<Camera>(camera);
        Transform place = _world.Get<Transform>(camera);
        CameraView view = CameraView.Create(lens, place, size.X, size.Y);
        Ray ray = view.GetRay(_input.MousePosition, size.X, size.Y);
        return _physics.RayCast(ray, MaxClickDistance, out hit);
    }

    private Entity FindFirst(in QueryDescription query)
    {
        Entity found = Entity.Null;
        _world.Query(in query, (Entity entity) =>
        {
            if (found == Entity.Null)
            {
                found = entity;
            }
        });
        return found;
    }
}
