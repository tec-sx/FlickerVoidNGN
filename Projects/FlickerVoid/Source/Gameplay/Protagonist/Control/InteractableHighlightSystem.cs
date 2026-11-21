using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Gameplay.Protagonist.Control;

using World = Arch.Core.World;

/// <summary>Outlines the interactables an <see cref="InteractableHighlight"/>'s interactor could use within its range, and takes the outline off the rest.</summary>
public sealed class InteractableHighlightSystem : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Highlighters = new QueryDescription().WithAll<InteractableHighlight, Interactor, Transform>();
    private static readonly QueryDescription Interactables = new QueryDescription().WithAll<ActiveInteractable, Transform>();

    private readonly List<Entity> _lit = [];
    private readonly List<Entity> _unlit = [];

    public InteractableHighlightSystem(World world)
        : base(world)
    {
    }

    public override void Update(in GameTime time)
    {
        bool found = false;
        InteractableHighlight highlight = default;
        Interactor interactor = default;
        Transform origin = default;
        World.Query(in Highlighters, (ref InteractableHighlight settings, ref Interactor settingsInteractor, ref Transform transform) =>
        {
            found = true;
            highlight = settings;
            interactor = settingsInteractor;
            origin = transform;
        });

        float reach = highlight.Range * highlight.Range;
        World.Query(in Interactables, (Entity entity, ref ActiveInteractable active, ref Transform transform) =>
        {
            bool usable = found && active.CanInteract && active.Definition.Accepts(interactor.Kind);
            float distance = Vector3.DistanceSquared(origin.Position, transform.Position);
            bool lit = usable && distance <= reach;
            bool outlined = World.Has<Outline>(entity);
            if (lit && !outlined)
            {
                _lit.Add(entity);
            }
            else if (!lit && outlined)
            {
                _unlit.Add(entity);
            }
        });

        Outline outline = new() { Color = highlight.Color };
        foreach (Entity entity in _lit)
        {
            World.Add(entity, outline);
        }

        foreach (Entity entity in _unlit)
        {
            World.Remove<Outline>(entity);
        }

        _lit.Clear();
        _unlit.Clear();
    }
}
