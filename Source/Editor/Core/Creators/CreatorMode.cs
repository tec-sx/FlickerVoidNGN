using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Creators;

/// <summary>
/// The viewport's mode while a creation is made (<see cref="CreatorPlacement"/>): everything but the creation is greyed or set aside,
/// only its parts can be picked, a work light follows the view, and undo, redo and save act on the creation's file. Leaving takes the
/// marks, the light and a temporary copy away again.
/// </summary>
public sealed class CreatorMode : IViewportMode, IDisposable
{
    /// <summary>How far the work light reaches past the view's pivot.</summary>
    private const float WorkLightReach = 5;

    private static readonly QueryDescription Everything = new();

    private readonly EditorScene _scene;
    private readonly EditorCamera _camera;
    private readonly Func<bool> _save;
    private readonly List<Entity> _marked = [];

    private CreatorMode(EditorScene scene, EditorCamera camera, Entity root, CreatorPlacement placement, UndoHistory history, Func<bool> save)
    {
        _scene = scene;
        _camera = camera;
        _save = save;
        Root = root;
        Placement = placement;
        History = history;
        World world = scene.Game.World;
        WorkLight = world.Create(new Transform(), new PointLight());
        Update();
        Mark();
        scene.Content.Changed += HandleChanged;
    }

    /// <summary>The creation being made: the one placed in the level, or the temporary copy.</summary>
    public Entity Root { get; }

    public CreatorPlacement Placement { get; }

    /// <summary>The light at the view that shows the creation however the level is lit; never saved.</summary>
    public Entity WorkLight { get; }

    /// <summary>The creation file's undo steps.</summary>
    public UndoHistory History { get; }

    /// <summary>The id of the part last clicked, or null.</summary>
    public string? SelectedPart { get; private set; }

    /// <summary>Raised when another part, or none, is selected.</summary>
    public event Action? PartSelected;

    /// <summary>Makes the creation placed in the level, greying the rest of it.</summary>
    public static CreatorMode InPlace(EditorScene scene, EditorCamera camera, Entity placed, UndoHistory history, Func<bool> save)
    {
        return new CreatorMode(scene, camera, placed, CreatorPlacement.InPlace, history, save);
    }

    /// <summary>
    /// Makes a creation alone: places a temporary entity with <paramref name="creation"/>, the component that names its file, at the
    /// origin, outside the scene, and sets the level aside.
    /// </summary>
    public static CreatorMode Alone<T>(EditorScene scene, EditorCamera camera, T creation, UndoHistory history, Func<bool> save)
        where T : struct
    {
        World world = scene.Game.World;
        Entity copy = world.Create(new Transform(), creation);
        scene.Game.Lifecycle.Start(world, copy);
        return new CreatorMode(scene, camera, copy, CreatorPlacement.Alone, history, save);
    }

    public bool Save() => _save();

    /// <summary>Selects the part of the creation under the mouse, whatever is in front of it, or none.</summary>
    public void PointerPressed(Vector2 pixel, int width, int height)
    {
        World world = _scene.Game.World;
        Ray ray = _camera.GetRay(pixel, width, height);
        Entity? picked = _scene.Picker.PickPart(world, ray, Root);
        string? id = null;
        if (picked is { } part)
        {
            id = world.Get<CreationPart>(part).Id;
        }

        SelectPart(id);
    }

    /// <summary>Selects the creation's part with the id, or none, as a click on it does.</summary>
    public void SelectPart(string? id)
    {
        if (id == SelectedPart)
        {
            return;
        }

        SelectedPart = id;
        PartSelected?.Invoke();
    }

    /// <summary>The entity of the selected part, or null when none is selected or the creation no longer has it.</summary>
    public Entity? FindSelectedPart()
    {
        CreationParts? parts = _scene.Game.Services.GetService<CreationParts>();
        if (SelectedPart is not { } id || parts is null)
        {
            return null;
        }

        Entity part = parts.Find(Root, id);
        if (part == Entity.Null)
        {
            return null;
        }

        return part;
    }

    public void PointerMoved(Vector2 pixel, int width, int height)
    {
    }

    public void PointerReleased()
    {
    }

    /// <summary>Moves the work light to the view, reaching past its pivot.</summary>
    public void Update()
    {
        World world = _scene.Game.World;
        Transform place = new() { Position = _camera.Position };
        world.Set(WorkLight, place);
        ref PointLight light = ref world.Get<PointLight>(WorkLight);
        light.Range = (_camera.PivotDistance * 2) + WorkLightReach;
    }

    /// <summary>Leaves the creator: takes the marks off the level and removes the work light and a temporary copy with its parts.</summary>
    public void Dispose()
    {
        _scene.Content.Changed -= HandleChanged;
        World world = _scene.Game.World;
        foreach (Entity entity in _marked)
        {
            if (world.IsAlive(entity))
            {
                Unmark(world, entity);
            }
        }

        _marked.Clear();
        if (world.IsAlive(WorkLight))
        {
            world.Destroy(WorkLight);
        }

        if (Placement == CreatorPlacement.Alone && world.IsAlive(Root))
        {
            _scene.Game.Lifecycle.Destroy(world, Root);
        }
    }

    /// <summary>Greys or sets aside every entity that is not the creation, its parts or the work light, and not marked yet.</summary>
    private void Mark()
    {
        World world = _scene.Game.World;
        CreationParts? parts = _scene.Game.Services.GetService<CreationParts>();
        IReadOnlyList<Entity> ownParts = parts?.FindAll(Root) ?? [];
        HashSet<Entity> own = [Root, WorkLight, .. ownParts];
        List<Entity> others = [];
        world.Query(in Everything, (Entity entity) =>
        {
            if (!own.Contains(entity) && !IsMarked(world, entity))
            {
                others.Add(entity);
            }
        });
        foreach (Entity entity in others)
        {
            if (Placement == CreatorPlacement.InPlace)
            {
                world.Add<Greyed>(entity);
            }
            else
            {
                world.Add<Undrawn>(entity);
            }

            _marked.Add(entity);
        }
    }

    private bool IsMarked(World world, Entity entity)
    {
        if (Placement == CreatorPlacement.InPlace)
        {
            return world.Has<Greyed>(entity);
        }

        return world.Has<Undrawn>(entity);
    }

    private void Unmark(World world, Entity entity)
    {
        if (Placement == CreatorPlacement.InPlace)
        {
            world.Remove<Greyed>(entity);
        }
        else
        {
            world.Remove<Undrawn>(entity);
        }
    }

    /// <summary>A file change may have spawned parts for other copies of the creation, which are marked like the rest.</summary>
    private void HandleChanged(string fullPath)
    {
        _marked.RemoveAll(entity => !_scene.Game.World.IsAlive(entity));
        Mark();
    }
}
