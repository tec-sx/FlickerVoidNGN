using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Creators;

/// <summary>
/// The viewport's mode while a creation is made (<see cref="CreatorPlacement"/>): everything but the creation is greyed or set aside,
/// only its parts can be picked, a work light follows the view, and undo, redo and save act on the creation's file. Leaving takes the
/// marks, the light and a temporary copy away again. A selected part's mesh can be entered (Tab): clicks then select its points,
/// edges or faces, and the part can't change until the mesh is left.
/// </summary>
public sealed class CreatorMode : IViewportMode, IDisposable
{
    /// <summary>How far the work light reaches past the view's pivot.</summary>
    private const float WorkLightReach = 5;

    private static readonly QueryDescription Everything = new();

    private readonly EditorScene _scene;
    private readonly EditorCamera _camera;
    private readonly Func<bool> _save;
    private readonly Func<string, SolidMesh?>? _findMesh;
    private SolidMesh? _meshSolid;
    private readonly List<Entity> _marked = [];

    private CreatorMode(EditorScene scene, EditorCamera camera, Entity root, CreatorPlacement placement, UndoHistory history, Func<bool> save, Func<string, SolidMesh?>? findMesh)
    {
        _scene = scene;
        _camera = camera;
        _save = save;
        _findMesh = findMesh;
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

    /// <summary>The selection in the mesh of the part being edited, or null while no mesh is entered.</summary>
    public MeshSelection? MeshSelection { get; private set; }

    /// <summary>The id of the part whose mesh is entered, or null.</summary>
    public string? MeshPart { get; private set; }

    /// <summary>Raised when a mesh is entered or left.</summary>
    public event Action? MeshModeChanged;

    /// <summary>
    /// Makes the creation placed in the level, greying the rest of it. <paramref name="findMesh"/> gives the mesh of a part by its id,
    /// for entering the mesh.
    /// </summary>
    public static CreatorMode InPlace(EditorScene scene, EditorCamera camera, Entity placed, UndoHistory history, Func<bool> save, Func<string, SolidMesh?>? findMesh = null)
    {
        return new CreatorMode(scene, camera, placed, CreatorPlacement.InPlace, history, save, findMesh);
    }

    /// <summary>
    /// Makes a creation alone: places a temporary entity with <paramref name="creation"/>, the component that names its file, at the
    /// origin, outside the scene, and sets the level aside.
    /// </summary>
    public static CreatorMode Alone<T>(EditorScene scene, EditorCamera camera, T creation, UndoHistory history, Func<bool> save, Func<string, SolidMesh?>? findMesh = null)
        where T : struct
    {
        World world = scene.Game.World;
        Entity copy = world.Create(new Transform(), creation);
        scene.Game.Lifecycle.Start(world, copy);
        return new CreatorMode(scene, camera, copy, CreatorPlacement.Alone, history, save, findMesh);
    }

    public bool Save() => _save();

    /// <summary>Selects the part of the creation under the mouse, whatever is in front of it, or none.</summary>
    public void PointerPressed(Vector2 pixel, int width, int height)
    {
        if (MeshSelection is not null)
        {
            PressMesh(pixel, width, height, additive: false, loop: false);
            return;
        }

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

    /// <summary>Selects the creation's part with the id, or none, as a click on it does; another part leaves the mesh.</summary>
    public void SelectPart(string? id)
    {
        if (id == SelectedPart)
        {
            return;
        }

        LeaveMesh();

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

    /// <summary>
    /// Enters the mesh of the selected part (Tab); false when no part is selected or the creation gives it no mesh. Clicks then select
    /// points, edges or faces, with no point, edge or face selected to begin with.
    /// </summary>
    public bool EnterMesh()
    {
        if (MeshSelection is not null)
        {
            return true;
        }

        if (SelectedPart is not { } id || _findMesh?.Invoke(id) is not { } solid)
        {
            return false;
        }

        MeshPart = id;
        _meshSolid = solid;
        MeshSelection = new MeshSelection(solid);
        MeshModeChanged?.Invoke();
        return true;
    }

    /// <summary>Leaves the mesh (Tab again), dropping its selection; does nothing when none is entered.</summary>
    public void LeaveMesh()
    {
        if (MeshSelection is null)
        {
            return;
        }

        MeshSelection = null;
        MeshPart = null;
        _meshSolid = null;
        Hover = null;
        MeshModeChanged?.Invoke();
    }

    /// <summary>
    /// Selects in the entered mesh what is under the mouse: with <paramref name="additive"/> (Shift) it joins the selection or leaves
    /// it, and with <paramref name="loop"/> (Alt) the loop through it is selected instead. Does nothing outside the mesh.
    /// </summary>
    public void PressMesh(Vector2 pixel, int width, int height, bool additive, bool loop)
    {
        if (MeshSelection is not { } selection || MeshPart is not { } id || _findMesh?.Invoke(id) is not { } solid)
        {
            return;
        }

        Entity? part = FindSelectedPart();
        if (part is not { } entity)
        {
            return;
        }

        Matrix4x4 toWorld = _scene.Game.World.Get<Transform>(entity).ToMatrix();
        MeshPicker picker = new(solid, toWorld, _camera, width, height);
        MeshHit? hit = picker.Pick(selection.Element, pixel);
        if (loop && hit is { } found)
        {
            selection.SelectLoop(found, additive);
            return;
        }

        selection.Select(hit, additive);
    }

    /// <summary>The point, edge or face of the entered mesh under the mouse, or null.</summary>
    public MeshHit? Hover { get; private set; }

    public void PointerMoved(Vector2 pixel, int width, int height)
    {
        Hover = FindMeshHit(pixel, width, height);
    }

    /// <summary>Adds the entered mesh, its selection and the hover to the overlay; nothing outside the mesh.</summary>
    public void DrawMesh(OverlayBuilder overlay)
    {
        if (MeshSelection is not { } selection || MeshPart is not { } id || _findMesh?.Invoke(id) is not { } solid)
        {
            return;
        }

        if (FindSelectedPart() is not { } part)
        {
            return;
        }

        Matrix4x4 toWorld = _scene.Game.World.Get<Transform>(part).ToMatrix();
        MeshOverlay.Draw(overlay, _camera, solid, toWorld, selection, Hover);
    }

    private MeshHit? FindMeshHit(Vector2 pixel, int width, int height)
    {
        if (MeshSelection is not { } selection || MeshPart is not { } id || _findMesh?.Invoke(id) is not { } solid)
        {
            return null;
        }

        if (FindSelectedPart() is not { } part)
        {
            return null;
        }

        Matrix4x4 toWorld = _scene.Game.World.Get<Transform>(part).ToMatrix();
        MeshPicker picker = new(solid, toWorld, _camera, width, height);
        return picker.Pick(selection.Element, pixel);
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
        LeaveMesh();
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

    /// <summary>A file change may have given the part another solid, which the selection no longer fits: it starts again empty, in the same kind; the mesh is left when the part is gone.</summary>
    private void RefreshMesh()
    {
        if (MeshPart is not { } id)
        {
            return;
        }

        SolidMesh? solid = _findMesh?.Invoke(id);
        if (solid is null)
        {
            LeaveMesh();
        }
        else if (solid != _meshSolid && MeshSelection is { } old)
        {
            _meshSolid = solid;
            MeshSelection selection = new(solid);
            selection.SetElement(old.Element);
            MeshSelection = selection;
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
        RefreshMesh();
        _marked.RemoveAll(entity => !_scene.Game.World.IsAlive(entity));
        Mark();
    }
}
