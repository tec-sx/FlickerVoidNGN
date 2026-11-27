using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Toggles;
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
public sealed partial class CreatorMode : IViewportMode, IDisposable
{
    /// <summary>How far the work light reaches past the view's pivot.</summary>
    private const float WorkLightReach = 5;

    private static readonly QueryDescription Everything = new();

    private readonly EditorScene _scene;
    private readonly EditorCamera _camera;
    private readonly Func<bool> _save;
    private readonly Func<string, SolidMesh?>? _findMesh;
    private readonly Action<string, SolidMesh>? _applyMesh;
    private readonly Func<string, bool>? _isMirrored;
    private readonly Action<string, bool>? _setMirror;
    private SolidMesh? _meshSolid;
    private SolidMesh? _dragSolid;
    private MeshElement _dragElement;
    private int[] _dragFaces = [];
    private SolidEdge[] _dragEdges = [];
    private MeshDragOptions _dragOptions;
    private bool _dragShown;
    private readonly List<Entity> _marked = [];

    private CreatorMode(EditorScene scene, EditorCamera camera, Entity root, CreatorPlacement placement, UndoHistory history, Func<bool> save, Func<string, SolidMesh?>? findMesh, Action<string, SolidMesh>? applyMesh, Func<string, bool>? isMirrored, Action<string, bool>? setMirror)
    {
        _scene = scene;
        _camera = camera;
        _save = save;
        _findMesh = findMesh;
        _applyMesh = applyMesh;
        _isMirrored = isMirrored;
        _setMirror = setMirror;
        Root = root;
        Placement = placement;
        History = history;
        World world = scene.Game.World;
        WorkLight = world.Create(new Transform(), new PointLight());
        if (placement == CreatorPlacement.Piece)
        {
            SelectedPart = PieceId;
            scene.Edited += RefreshMesh;
        }

        Update();
        Mark();
        scene.Content.Changed += HandleChanged;
    }

    /// <summary>The part id of a level's greybox being shaped, which is its own only part.</summary>
    public const string PieceId = "piece";

    /// <summary>The creation being made: the one placed in the level, the temporary copy, or the greybox being shaped.</summary>
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
    public static CreatorMode InPlace(EditorScene scene, EditorCamera camera, Entity placed, UndoHistory history, Func<bool> save, Func<string, SolidMesh?>? findMesh = null, Action<string, SolidMesh>? applyMesh = null, Func<string, bool>? isMirrored = null, Action<string, bool>? setMirror = null)
    {
        return new CreatorMode(scene, camera, placed, CreatorPlacement.InPlace, history, save, findMesh, applyMesh, isMirrored, setMirror);
    }

    /// <summary>
    /// Makes a creation alone: places a temporary entity with <paramref name="creation"/>, the component that names its file, at the
    /// origin, outside the scene, and sets the level aside.
    /// </summary>
    public static CreatorMode Alone<T>(EditorScene scene, EditorCamera camera, T creation, UndoHistory history, Func<bool> save, Func<string, SolidMesh?>? findMesh = null, Action<string, SolidMesh>? applyMesh = null, Func<string, bool>? isMirrored = null, Action<string, bool>? setMirror = null)
        where T : struct
    {
        World world = scene.Game.World;
        Entity copy = world.Create(new Transform(), creation);
        scene.Game.Lifecycle.Start(world, copy);
        return new CreatorMode(scene, camera, copy, CreatorPlacement.Alone, history, save, findMesh, applyMesh, isMirrored, setMirror);
    }

    /// <summary>
    /// Shapes a greybox of the level in place, greying the rest: its only part is itself (<see cref="PieceId"/>), always selected, its
    /// mesh is kept on the greybox, and undo, redo and save are the level's.
    /// </summary>
    public static CreatorMode Piece(EditorScene scene, EditorCamera camera, Entity greybox)
    {
        PieceShape shape = new(scene, greybox);
        return new CreatorMode(scene, camera, greybox, CreatorPlacement.Piece, scene.History, scene.Save, _ => shape.Find(), (_, mesh) => shape.Apply(mesh), null, null);
    }

    public bool Save() => _save();

    /// <summary>Selects the part of the creation under the mouse, whatever is in front of it, or none.</summary>
    public void PointerPressed(Vector2 pixel, int width, int height)
    {
        if (PressDoor(pixel, width, height))
        {
            return;
        }

        if (MeshSelection is not null)
        {
            if (!BeginGizmoDrag(pixel, width, height))
            {
                PressMesh(pixel, width, height, additive: false, loop: false);
            }

            return;
        }

        if (Placement == CreatorPlacement.Piece)
        {
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
        if (Placement == CreatorPlacement.Piece)
        {
            return;
        }

        if (id == SelectedPart)
        {
            if (id is not null && Door is null)
            {
                EditDoor(id);
            }

            return;
        }

        LeaveMesh();
        CancelDoor();

        SelectedPart = id;
        if (id is not null)
        {
            EditDoor(id);
        }

        PartSelected?.Invoke();
    }

    /// <summary>The entity of the selected part, or null when none is selected or the creation no longer has it.</summary>
    public Entity? FindSelectedPart()
    {
        if (Placement == CreatorPlacement.Piece)
        {
            return _scene.Game.World.IsAlive(Root) ? Root : null;
        }

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

        EndMeshDrag(keep: false);

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

    /// <summary>Whether the entered part's mirror switch is on: its edits are made on both sides of its centre line.</summary>
    public bool Mirror => MeshPart is { } id && _isMirrored?.Invoke(id) == true;

    /// <summary>Turns the entered part's mirror switch on or off, kept in its file as one undo step; false outside the mesh.</summary>
    public bool SetMirror(bool on)
    {
        if (MeshSelection is null || MeshPart is not { } id || _setMirror is null)
        {
            return false;
        }

        _setMirror(id, on);
        MeshModeChanged?.Invoke();
        return true;
    }

    /// <summary>Moves the selected points by an offset in the part's own space, as one undo step; false when none is selected.</summary>
    public bool MoveMesh(Vector3 offset) => EditMesh(solid => MeshEdit.Move(solid, MeshSelection!.Points, offset));

    /// <summary>Turns the selected points about their middle by degrees about the part's X, Y and Z, as one undo step.</summary>
    public bool RotateMesh(Vector3 degrees) => EditMesh(solid => MeshEdit.Rotate(solid, MeshSelection!.Points, degrees));

    /// <summary>Scales the selected points about their middle by a factor along each of the part's axes, as one undo step.</summary>
    public bool ScaleMesh(Vector3 factor) => EditMesh(solid => MeshEdit.Scale(solid, MeshSelection!.Points, factor));

    /// <summary>
    /// Extrudes the selected faces by a distance along their normal as one undo step, leaving the pushed faces selected in face mode;
    /// false when no face is selected or all are.
    /// </summary>
    public bool ExtrudeMesh(float distance) => ReplaceMesh((solid, selected) => MeshExtrude.Faces(solid, GetFaces(solid, selected), distance));

    /// <summary>
    /// Insets the selected faces by a thickness as one undo step, leaving the inner faces selected in face mode; false when no face
    /// is selected or all are.
    /// </summary>
    public bool InsetMesh(float thickness) => ReplaceMesh((solid, selected) => MeshInset.Faces(solid, GetFaces(solid, selected), thickness));

    /// <summary>Opens or shuts the door part of the vehicle at once, to see it swing; false for a part without a joint and toggle.</summary>
    public bool SwingDoor(string id)
    {
        if (Placement == CreatorPlacement.Piece)
        {
            return false;
        }

        Entity part = _scene.Game.Services.GetService<CreationParts>()!.Find(Root, id);
        World world = _scene.Game.World;
        if (!world.IsAlive(part) || !world.Has<PartJoint, Toggle>(part))
        {
            return false;
        }

        Toggle toggle = world.Get<Toggle>(part);
        toggle.Open = !toggle.Open;
        world.Set(part, toggle);
        _scene.Game.Services.GetService<JointSystem>()!.Snap(part, toggle.Open);
        return true;
    }

    /// <summary>
    /// Cuts the selected faces into the solid (C) as one undo step: a pocket to a depth, or through it. The floor of a pocket stays
    /// selected. False when no face is selected, or a through cut can't be made (no Manifold library, nothing left).
    /// </summary>
    public bool CutMesh(float depth, bool through) => ReplaceMesh((solid, selected) => MeshCut.Faces(solid, GetFaces(solid, selected), depth, through));

    /// <summary>
    /// Bevels the selected edges (B) as one undo step: each becomes a strip of the width on each face, in rows, flat or
    /// <paramref name="round"/>; the strips stay selected. False when no edge is selected or a point of one can't take it.
    /// </summary>
    public bool BevelMesh(float width, int rows, bool round) => ReplaceMesh((solid, selected) => MeshBevel.Edges(solid, GetEdges(solid, selected), width, rows, round));

    /// <summary>
    /// Cuts loops across the ring of quads through the edge of the face under the mouse nearest it (L), as one undo step,
    /// leaving the new edges selected; <paramref name="slide"/> (-1 to 1) moves them along the ring. False when the mouse is over no
    /// face or the ring crosses no quad.
    /// </summary>
    public bool LoopCutMesh(Vector2 pixel, int width, int height, int cuts, float slide)
    {
        if (MeshSelection is null || MeshPart is not { } id || FindSelectedPart() is not { } part || _findMesh?.Invoke(id) is not { } solid)
        {
            return false;
        }

        Matrix4x4 toWorld = _scene.Game.World.Get<Transform>(part).ToMatrix();
        MeshPicker picker = new(solid, toWorld, _camera, width, height);
        MeshHit? hit = picker.Pick(MeshElement.Face, pixel);
        if (hit is not { } found)
        {
            return false;
        }

        return ReplaceMesh((current, _) => CutLoops(current, found.Edge, cuts, slide));
    }

    /// <summary>The tool whose amount is being dragged, or null.</summary>
    public MeshDragTool? MeshDrag { get; private set; }

    /// <summary>
    /// Starts dragging a tool over the selection: every <see cref="DragMesh"/> then shows the tool's work at an amount, made from the
    /// mesh as it is now, until <see cref="EndMeshDrag"/> keeps the last as one undo step or puts the mesh back. False when the tool
    /// has nothing selected to work on (faces, or edges for a bevel).
    /// </summary>
    public bool BeginMeshDrag(MeshDragTool tool, MeshDragOptions options)
    {
        EndMeshDrag(keep: true);
        if (MeshSelection is not { } selection || MeshPart is not { } id || _applyMesh is null || _findMesh?.Invoke(id) is not { } solid)
        {
            return false;
        }

        bool bevel = tool == MeshDragTool.Bevel;
        if ((bevel && selection.Edges.Count == 0) || (!bevel && selection.Faces.Count == 0))
        {
            return false;
        }

        _dragSolid = solid;
        _dragElement = selection.Element;
        _dragFaces = [.. selection.Faces];
        _dragEdges = [.. selection.Edges];
        _dragOptions = options;
        _dragShown = false;
        MeshDrag = tool;
        History.BeginGroup(tool.ToString());
        return true;
    }

    /// <summary>Shows the dragged tool's work at the amount; false, with the mesh as it was, when the tool can make nothing of it.</summary>
    public bool DragMesh(float amount)
    {
        if (MeshDrag is not { } tool || MeshPart is not { } id || _dragSolid is not { } start)
        {
            return false;
        }

        MeshSelection selection = StartOfDrag(start);
        MeshResult? result = null;
        if (amount > 0 || (tool == MeshDragTool.Extrude && amount < 0))
        {
            result = RunDragTool(tool, start, selection, amount);
        }

        if (result is null)
        {
            if (_dragShown)
            {
                _dragShown = false;
                _meshSolid = start;
                MeshSelection = selection;
                _applyMesh!(id, start);
            }

            return false;
        }

        MeshSelection next = new(result.Solid);
        if (result.Edges.Count > 0)
        {
            next.SelectEdges(result.Edges);
        }
        else
        {
            next.SelectFaces(result.Faces);
        }

        _dragShown = true;
        _meshSolid = result.Solid;
        MeshSelection = next;
        _applyMesh!(id, result.Solid);
        return true;
    }

    /// <summary>Ends the drag: keeps what it shows as one undo step, or puts the mesh and its selection back.</summary>
    public void EndMeshDrag(bool keep)
    {
        if (MeshDrag is null)
        {
            return;
        }

        MeshDrag = null;
        SolidMesh start = _dragSolid!;
        _dragSolid = null;
        if (keep)
        {
            History.EndGroup();
        }
        else
        {
            if (_dragShown && MeshPart is { } id)
            {
                _meshSolid = start;
                _applyMesh!(id, start);
            }

            History.CancelGroup();
            if (MeshSelection is not null)
            {
                _meshSolid = start;
                MeshSelection = StartOfDrag(start);
            }
        }

        MeshModeChanged?.Invoke();
    }

    /// <summary>How far a drag from one pixel to another sets a tool's amount, in metres: the way up the screen, at the view's pivot.</summary>
    public float GetMeshDragAmount(Vector2 from, Vector2 to, int height)
    {
        float perPixel = _camera.WorldUnitsPerPixel(_camera.Pivot, height);
        return (from.Y - to.Y) * perPixel;
    }

    private MeshSelection StartOfDrag(SolidMesh start)
    {
        MeshSelection selection = new(start);
        selection.SetElement(_dragElement);
        if (_dragElement == MeshElement.Edge)
        {
            selection.SelectEdges(_dragEdges);
        }
        else
        {
            selection.SelectFaces(_dragFaces);
        }

        return selection;
    }

    private MeshResult? RunDragTool(MeshDragTool tool, SolidMesh solid, MeshSelection selection, float amount)
    {
        switch (tool)
        {
            case MeshDragTool.Extrude:
                return MeshExtrude.Faces(solid, GetFaces(solid, selection), amount);
            case MeshDragTool.Inset:
                return MeshInset.Faces(solid, GetFaces(solid, selection), amount);
            case MeshDragTool.Cut:
                return MeshCut.Faces(solid, GetFaces(solid, selection), amount, through: false);
            default:
                return MeshBevel.Edges(solid, GetEdges(solid, selection), amount, _dragOptions.Rows, _dragOptions.Round);
        }
    }

    private IReadOnlySet<int> GetFaces(SolidMesh solid, MeshSelection selected)
    {
        if (Mirror)
        {
            return MeshMirror.ExpandFaces(solid, selected.Faces);
        }

        return selected.Faces;
    }

    private IReadOnlySet<SolidEdge> GetEdges(SolidMesh solid, MeshSelection selected)
    {
        if (Mirror)
        {
            return MeshMirror.ExpandEdges(solid, selected.Edges);
        }

        return selected.Edges;
    }

    // With the mirror on, the ring across the centre line from the edge's is cut the same, unless it is the same ring.
    private MeshResult? CutLoops(SolidMesh solid, SolidEdge edge, int cuts, float slide)
    {
        MeshResult? first = MeshLoopCut.Cut(solid, edge, cuts, slide);
        if (first is null || !Mirror)
        {
            return first;
        }

        int[] twins = MeshMirror.FindTwins(solid);
        if (twins[edge.A] < 0 || twins[edge.B] < 0)
        {
            return first;
        }

        SolidEdge twin = SolidEdge.Of(twins[edge.A], twins[edge.B]);
        SolidTopology topology = new(first.Solid);
        if (twin == edge || !topology.Edges.Contains(twin) || new SolidTopology(solid).GetEdgeRing(edge).Contains(twin))
        {
            return first;
        }

        MeshResult? second = null;
        foreach (float candidate in new[] { slide, -slide })
        {
            MeshResult? tried = MeshLoopCut.Cut(first.Solid, twin, cuts, candidate);
            if (tried is not null && MeshMirror.FindTwins(tried.Solid).All(point => point >= 0))
            {
                second = tried;
                break;
            }
        }

        if (second is null)
        {
            return first;
        }

        return second with { Edges = [.. first.Edges, .. second.Edges] };
    }

    private bool ReplaceMesh(Func<SolidMesh, MeshSelection, MeshResult?> tool)
    {
        if (MeshSelection is not { } selection || MeshPart is not { } id || _applyMesh is null || _findMesh?.Invoke(id) is not { } solid)
        {
            return false;
        }

        MeshResult? result = tool(solid, selection);
        if (result is null)
        {
            return false;
        }

        MeshSelection next = new(result.Solid);
        if (result.Edges.Count > 0)
        {
            next.SelectEdges(result.Edges);
        }
        else
        {
            next.SelectFaces(result.Faces);
        }

        _meshSolid = result.Solid;
        MeshSelection = next;
        _applyMesh(id, result.Solid);
        MeshModeChanged?.Invoke();
        return true;
    }

    private bool EditMesh(Func<SolidMesh, SolidMesh> edit)
    {
        if (MeshSelection is not { } selection || selection.Points.Count == 0 || MeshPart is not { } id || _applyMesh is null)
        {
            return false;
        }

        if (_findMesh?.Invoke(id) is not { } solid)
        {
            return false;
        }

        SolidMesh edited = edit(solid);
        if (Mirror)
        {
            edited = MeshMirror.Follow(solid, edited, selection.Points);
        }

        _meshSolid = edited;
        selection.TryFollow(edited);
        _applyMesh(id, edited);
        return true;
    }

    /// <summary>The point, edge or face of the entered mesh under the mouse, or null.</summary>
    public MeshHit? Hover { get; private set; }

    public void PointerMoved(Vector2 pixel, int width, int height)
    {
        if (Door is not null)
        {
            MoveDoor(pixel, width, height);
            return;
        }

        if (Gizmo is { IsDragging: true })
        {
            DragGizmo(pixel, width, height);
            return;
        }

        if (Gizmo is { } gizmo && TryGetGizmoTarget(out Transform target))
        {
            gizmo.Hover(_camera, target, pixel, width, height);
        }

        Hover = FindMeshHit(pixel, width, height);
    }

    /// <summary>The viewport's gizmo, which moves, turns or scales the selected points, edges or faces of the entered mesh about their middle.</summary>
    public TransformGizmo? Gizmo { get; set; }

    private Transform _gizmoStart;
    private Transform _gizmoPart;
    private SolidMesh? _gizmoSolid;

    /// <summary>Where the gizmo stands: the middle of the selected points, in the part's turn; false with none selected or no gizmo.</summary>
    public bool TryGetGizmoTarget(out Transform target)
    {
        target = default;
        if (Gizmo is null || MeshSelection is not { Points.Count: > 0 } selection || MeshPart is not { } id || _findMesh?.Invoke(id) is not { } solid || FindSelectedPart() is not { } part)
        {
            return false;
        }

        Transform placed = _scene.Game.World.Get<Transform>(part);
        Vector3 centre = MeshEdit.GetCentre(solid, selection.Points);
        Matrix4x4 toWorld = placed.ToMatrix();
        target = new Transform { Position = Vector3.Transform(centre, toWorld), Rotation = placed.Rotation, Scale = Vector3.One };
        return true;
    }

    /// <summary>Starts dragging the gizmo handle under the mouse over the selected mesh points, as one undo step; false when no handle is there.</summary>
    public bool BeginGizmoDrag(Vector2 pixel, int width, int height)
    {
        if (Gizmo is not { Mode: not GizmoMode.Select } gizmo || !TryGetGizmoTarget(out Transform target) || MeshPart is not { } id || _findMesh?.Invoke(id) is not { } solid)
        {
            return false;
        }

        if (!gizmo.BeginDrag(_camera, target, pixel, width, height))
        {
            return false;
        }

        _gizmoStart = target;
        _gizmoPart = _scene.Game.World.Get<Transform>(FindSelectedPart()!.Value);
        _gizmoSolid = solid;
        History.BeginGroup(gizmo.Mode.ToString());
        return true;
    }

    /// <summary>Adds the gizmo on the selected mesh points to the overlay.</summary>
    public void DrawGizmo(OverlayBuilder overlay, int viewportHeight)
    {
        if (Gizmo is { } gizmo && TryGetGizmoTarget(out Transform target))
        {
            gizmo.Draw(overlay, _camera, target, viewportHeight);
        }
    }

    private void DragGizmo(Vector2 pixel, int width, int height)
    {
        if (Gizmo is not { } gizmo || _gizmoSolid is not { } start || MeshSelection is not { } selection || MeshPart is not { } id || _applyMesh is null)
        {
            return;
        }

        Transform dragged = gizmo.Drag(_camera, pixel, width, height);
        Quaternion partTurn = _gizmoPart.Rotation;
        Quaternion inverse = Quaternion.Inverse(partTurn);
        SolidMesh edited;
        switch (gizmo.Mode)
        {
            case GizmoMode.Move:
                Vector3 local = Vector3.Transform(dragged.Position - _gizmoStart.Position, inverse);
                edited = MeshEdit.Move(start, selection.Points, local / SafeScale(_gizmoPart.Scale));
                break;
            case GizmoMode.Rotate:
                Quaternion world = dragged.Rotation * Quaternion.Inverse(_gizmoStart.Rotation);
                edited = MeshEdit.Rotate(start, selection.Points, Quaternion.Normalize(inverse * world * partTurn));
                break;
            default:
                edited = MeshEdit.Scale(start, selection.Points, dragged.Scale);
                break;
        }

        if (Mirror)
        {
            edited = MeshMirror.Follow(start, edited, selection.Points);
        }

        _meshSolid = edited;
        selection.TryFollow(edited);
        _applyMesh(id, edited);
    }

    private static Vector3 SafeScale(Vector3 scale)
    {
        return new Vector3(scale.X == 0 ? 1 : scale.X, scale.Y == 0 ? 1 : scale.Y, scale.Z == 0 ? 1 : scale.Z);
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
        Door?.EndDrag();
        if (Gizmo is { IsDragging: true } gizmo)
        {
            gizmo.EndDrag();
            _gizmoSolid = null;
            History.EndGroup();
        }
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
        CancelDoor();
        LeaveMesh();
        _scene.Edited -= RefreshMesh;
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
            if (Placement == CreatorPlacement.Alone)
            {
                world.Add<Undrawn>(entity);
            }
            else
            {
                world.Add<Greyed>(entity);
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
            if (old.TryFollow(solid))
            {
                return;
            }

            MeshSelection selection = new(solid);
            selection.SetElement(old.Element);
            MeshSelection = selection;
        }
    }

    private bool IsMarked(World world, Entity entity)
    {
        if (Placement == CreatorPlacement.Alone)
        {
            return world.Has<Undrawn>(entity);
        }

        return world.Has<Greyed>(entity);
    }

    private void Unmark(World world, Entity entity)
    {
        if (Placement == CreatorPlacement.Alone)
        {
            world.Remove<Undrawn>(entity);
        }
        else
        {
            world.Remove<Greyed>(entity);
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
