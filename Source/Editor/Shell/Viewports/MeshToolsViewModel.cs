using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Viewports;

/// <summary>The tool bar of the mesh mode: the element kind, the tools with their numbers, the mirror switch and the door test.</summary>
public sealed partial class MeshToolsViewModel : ObservableObject
{
    private readonly Func<CreatorMode?> _creator;
    private readonly Action<string> _report;
    private readonly MeshTransformBar _transform = new();
    private Vector2 _pointer;
    private (int Width, int Height) _size = (1, 1);
    private Vector2 _dragFrom;
    private bool _dragSettled;
    private bool _dragWrites;

    public MeshToolsViewModel(Func<CreatorMode?> creator, Action<string> report)
    {
        _creator = creator;
        _report = report;
        Door = new DoorToolViewModel(creator);
    }

    /// <summary>The numbers of the door tool.</summary>
    public DoorToolViewModel Door { get; }

    /// <summary>Whether the door tool is in hand: a door follows the mouse or waits for Apply.</summary>
    public bool IsDoorTool => _creator()?.Door is not null;

    /// <summary>Whether a tool waits for Apply or Cancel: a dragged mesh tool or the door.</summary>
    public bool IsConfirming => IsDragging || IsDoorTool;

    /// <summary>Whether the door tool can start in this creation.</summary>
    public bool CanDoor => _creator()?.CanPlaceDoor == true;

    /// <summary>Starts the door tool (D), or ends it when it is in hand.</summary>
    [RelayCommand]
    public void ToggleDoor()
    {
        if (_creator() is not { } creator)
        {
            return;
        }

        if (IsDoorTool)
        {
            creator.CancelDoor();
            return;
        }

        if (!creator.BeginDoor())
        {
            _report("Doors need Manifold's library, built by Source/Native/Manifold.");
            return;
        }

        _report("Door: move the mouse onto a wall and click to place it, drag the red handles for the cut's depth, Enter applies, Escape cancels.");
    }

    [ObservableProperty]
    public partial double Amount { get; set; } = 0.25;

    [ObservableProperty]
    public partial double Thickness { get; set; } = 0.1;

    [ObservableProperty]
    public partial double Rows { get; set; } = 1;

    [ObservableProperty]
    public partial double Cuts { get; set; } = 1;

    [ObservableProperty]
    public partial double Slide { get; set; }

    [ObservableProperty]
    public partial bool Round { get; set; }

    [ObservableProperty]
    public partial bool Through { get; set; }

    public double TransformX
    {
        get => _transform.Values.X;
        set => SetTransform(_transform.Values with { X = (float)value });
    }

    public double TransformY
    {
        get => _transform.Values.Y;
        set => SetTransform(_transform.Values with { Y = (float)value });
    }

    public double TransformZ
    {
        get => _transform.Values.Z;
        set => SetTransform(_transform.Values with { Z = (float)value });
    }

    public MeshTransformTool TransformTool
    {
        get => _transform.Tool;
        set
        {
            _transform.Tool = value;
            NotifyTransform();
        }
    }

    /// <summary>Whether a creator is open, which shows the Edit mesh button.</summary>
    public bool IsCreating => _creator() is not null;

    public bool IsMeshMode => _creator()?.MeshSelection is not null;

    public MeshElement Element
    {
        get => _creator()?.MeshSelection?.Element ?? MeshElement.Point;
        set
        {
            _creator()?.MeshSelection?.SetElement(value);
            Refresh();
        }
    }

    public bool IsPoint
    {
        get => Element == MeshElement.Point;
        set => SetElement(value, MeshElement.Point);
    }

    public bool IsEdge
    {
        get => Element == MeshElement.Edge;
        set => SetElement(value, MeshElement.Edge);
    }

    public bool IsFace
    {
        get => Element == MeshElement.Face;
        set => SetElement(value, MeshElement.Face);
    }

    public int TransformToolIndex
    {
        get => (int)TransformTool;
        set => TransformTool = (MeshTransformTool)value;
    }

    public bool Mirror
    {
        get => _creator()?.Mirror == true;
        set => _creator()?.SetMirror(value);
    }

    /// <summary>The tool whose amount is following the mouse, or null.</summary>
    public MeshDragTool? ActiveTool => _creator()?.MeshDrag;

    /// <summary>Whether the Tool tab has something to show: the mesh is entered.</summary>
    public bool HasToolOptions => IsMeshMode || IsDoorTool;

    /// <summary>The Tool tab's heading: the tool in hand, or what the gizmo does when none is.</summary>
    public string ToolName
    {
        get
        {
            if (IsDoorTool)
            {
                return "Door";
            }

            return ActiveTool?.ToString() ?? "Selection";
        }
    }

    public bool ShowsAmount => ActiveTool is MeshDragTool.Extrude or MeshDragTool.Cut;

    public bool ShowsCutThrough => ActiveTool == MeshDragTool.Cut;

    public bool ShowsThickness => ActiveTool is MeshDragTool.Inset or MeshDragTool.Bevel or MeshDragTool.Hollow;

    public bool ShowsBevel => ActiveTool == MeshDragTool.Bevel;

    /// <summary>What the thickness is called for the held tool: a hollow's wall, else a width.</summary>
    public string ThicknessName => ActiveTool == MeshDragTool.Hollow ? "Wall" : "Width";

    /// <summary>Whether no tool is held, so the options of the tools that act at once and the numeric transform show.</summary>
    public bool ShowsResting => IsMeshMode && ActiveTool is null && !IsDoorTool;

    public void Refresh()
    {
        OnPropertyChanged(nameof(ActiveTool));
        OnPropertyChanged(nameof(IsDoorTool));
        OnPropertyChanged(nameof(IsConfirming));
        OnPropertyChanged(nameof(CanDoor));
        Door.Refresh();
        OnPropertyChanged(nameof(HasToolOptions));
        OnPropertyChanged(nameof(ToolName));
        OnPropertyChanged(nameof(ShowsAmount));
        OnPropertyChanged(nameof(ShowsCutThrough));
        OnPropertyChanged(nameof(ShowsThickness));
        OnPropertyChanged(nameof(ThicknessName));
        OnPropertyChanged(nameof(ShowsBevel));
        OnPropertyChanged(nameof(ShowsResting));
        OnPropertyChanged(nameof(IsCreating));
        OnPropertyChanged(nameof(IsDragging));
        OnPropertyChanged(nameof(IsFollowing));
        OnPropertyChanged(nameof(IsMeshMode));
        OnPropertyChanged(nameof(Element));
        OnPropertyChanged(nameof(IsPoint));
        OnPropertyChanged(nameof(IsEdge));
        OnPropertyChanged(nameof(IsFace));
        OnPropertyChanged(nameof(Mirror));
    }

    /// <summary>Whether a tool's amount is following the mouse.</summary>
    public bool IsDragging => _creator()?.MeshDrag is not null;

    /// <summary>Remembers where the mouse is, for the tools that act on what is under it, and sets a dragged tool's amount by how far up it moved.</summary>
    public void Point(Vector2 pixel, int width, int height)
    {
        _pointer = pixel;
        _size = (width, height);
        if (_dragSettled || _creator() is not { MeshDrag: { } tool } creator)
        {
            return;
        }

        float amount = creator.GetMeshDragAmount(_dragFrom, pixel, height);
        if (tool != MeshDragTool.Extrude)
        {
            amount = MathF.Max(amount, 0);
        }

        creator.DragMesh(amount);
        _dragWrites = true;
        double shown = Math.Round(amount, 3);
        if (UsesThickness(tool))
        {
            Thickness = shown;
        }
        else
        {
            Amount = shown;
        }

        _dragWrites = false;
    }

    /// <summary>
    /// Starts a tool whose amount the mouse sets (X, I, C, B): moving it up makes more, a click keeps it, a right-click or Escape
    /// cancels, and a number typed in the tool's box sets it exactly.
    /// </summary>
    public void BeginDrag(MeshDragTool tool)
    {
        if (_creator() is not { } creator)
        {
            return;
        }

        MeshDragOptions options = new((int)Rows, Round);
        if (!creator.BeginMeshDrag(tool, options))
        {
            string what = tool == MeshDragTool.Bevel ? "edges" : "faces";
            _report($"Select {what} first.");
            return;
        }

        _dragFrom = _pointer;
        _dragSettled = false;
        _report($"{tool}: move the mouse up or down and click to set it, then tune it in the Tool tab; Enter applies, Escape cancels.");
        Refresh();
    }

    /// <summary>Keeps what the pending tool shows; false when no tool is pending.</summary>
    public bool ConfirmDrag() => EndDrag(keep: true);

    /// <summary>Whether the pending tool's amount still follows the mouse; a click or a typed number lets go of it.</summary>
    public bool IsFollowing => IsDragging && !_dragSettled;

    /// <summary>
    /// A click in the view while a tool is pending: the first one stops the mouse setting its amount, so its numbers can be tuned in
    /// the Tool tab before Apply (Enter); the tool stays pending either way. False when no tool is pending, so the click selects.
    /// </summary>
    public bool Settle()
    {
        if (!IsDragging)
        {
            return false;
        }

        if (!_dragSettled)
        {
            _dragSettled = true;
            _report($"{ToolName}: tune it in the Tool tab, then Enter or Apply keeps it and Escape cancels.");
            OnPropertyChanged(nameof(IsFollowing));
        }

        return true;
    }

    /// <summary>The confirm bar's Apply (Enter).</summary>
    [RelayCommand]
    public void Apply()
    {
        if (IsDoorTool)
        {
            ApplyDoor();
            return;
        }

        ConfirmDrag();
    }

    private void ApplyDoor()
    {
        if (_creator() is not { } creator)
        {
            return;
        }

        if (!creator.ApplyDoor())
        {
            _report("Click a wall to place the door first.");
        }
    }

    /// <summary>The confirm bar's Cancel (Escape).</summary>
    [RelayCommand]
    public void Cancel()
    {
        if (_creator()?.CancelDoor() == true)
        {
            return;
        }

        CancelDrag();
    }

    /// <summary>Puts the mesh back as it was before the dragged tool; false when no tool is dragged.</summary>
    public bool CancelDrag() => EndDrag(keep: false);

    [RelayCommand]
    public void DragExtrude() => BeginDrag(MeshDragTool.Extrude);

    [RelayCommand]
    public void DragInset() => BeginDrag(MeshDragTool.Inset);

    /// <summary>A pocket follows the mouse; a through cut has no amount and is made at once.</summary>
    [RelayCommand]
    public void DragCut()
    {
        if (Through)
        {
            Cut();
        }
        else
        {
            BeginDrag(MeshDragTool.Cut);
        }
    }

    [RelayCommand]
    public void DragBevel() => BeginDrag(MeshDragTool.Bevel);

    /// <summary>Hollows the entered mesh to a wall the mouse sets; the selected faces are left open.</summary>
    [RelayCommand]
    public void DragHollow() => BeginDrag(MeshDragTool.Hollow);

    /// <summary>The right-click menu of the mesh: the tools, then the switches with what each is set to.</summary>
    public IReadOnlyList<MenuChoice> GetMenu()
    {
        const string Tools = "Tools";
        const string Switches = "Switches";
        return
        [
            new MenuChoice("Extrude", DragExtrude, Tools, null, "X"),
            new MenuChoice("Inset", DragInset, Tools, null, "I"),
            new MenuChoice("Cut", DragCut, Tools, null, "C"),
            new MenuChoice("Bevel", DragBevel, Tools, null, "B"),
            new MenuChoice("Loop Cut", LoopCut, Tools, null, "L"),
            new MenuChoice("Hollow", DragHollow, Tools, null, "H"),
            new MenuChoice(Describe("Mirror", Mirror), () => Mirror = !Mirror, Switches),
            new MenuChoice(Describe("Round Bevels", Round), () => Round = !Round, Switches),
            new MenuChoice(Describe("Cut Through", Through), () => Through = !Through, Switches),
        ];
    }

    partial void OnAmountChanged(double value) => TypeDragAmount(value, thickness: false);

    partial void OnThicknessChanged(double value) => TypeDragAmount(value, thickness: true);

    partial void OnRowsChanged(double value) => RefreshDragOptions();

    partial void OnRoundChanged(bool value) => RefreshDragOptions();

    // Rows and roundness reach a pending bevel at once.
    private void RefreshDragOptions()
    {
        MeshDragOptions options = new((int)Rows, Round);
        _creator()?.SetMeshDragOptions(options);
    }

    private static string Describe(string name, bool on) => on ? $"{name}: On" : $"{name}: Off";

    private static bool UsesThickness(MeshDragTool tool) => tool is MeshDragTool.Inset or MeshDragTool.Bevel or MeshDragTool.Hollow;

    // A number typed while its tool is dragged sets the amount, and the mouse lets go of it.
    private void TypeDragAmount(double value, bool thickness)
    {
        if (_dragWrites || _creator() is not { MeshDrag: { } tool } creator || UsesThickness(tool) != thickness)
        {
            return;
        }

        _dragSettled = true;
        creator.DragMesh((float)value);
    }

    private bool EndDrag(bool keep)
    {
        if (_creator() is not { MeshDrag: not null } creator)
        {
            return false;
        }

        creator.EndMeshDrag(keep);
        _report(keep ? "Kept." : "Cancelled.");
        Refresh();
        return true;
    }

    [RelayCommand]
    public void Enter()
    {
        if (_creator() is not { } creator)
        {
            return;
        }

        if (!creator.EnterMesh())
        {
            _report("Click a part of the creation first (the body, say), then Edit mesh or Tab.");
        }
    }

    [RelayCommand]
    public void Leave() => _creator()?.LeaveMesh();

    /// <summary>The Edit mesh button: enters the selected part's mesh, or leaves the mesh being edited (Tab).</summary>
    [RelayCommand]
    public void ToggleMesh()
    {
        if (IsMeshMode)
        {
            Leave();
        }
        else
        {
            Enter();
        }

        Refresh();
    }

    [RelayCommand]
    public void Extrude() => Run(creator => creator.ExtrudeMesh((float)Amount), "Select faces to extrude.");

    [RelayCommand]
    public void Inset() => Run(creator => creator.InsetMesh((float)Thickness), "Select faces to inset.");

    [RelayCommand]
    public void Cut() => Run(creator => creator.CutMesh((float)Amount, Through), "Select faces to cut, or the cut can't be made.");

    [RelayCommand]
    public void Bevel() => Run(creator => creator.BevelMesh((float)Thickness, (int)Rows, Round), "Select edges to bevel, or one can't take it.");

    [RelayCommand]
    public void LoopCut() => Run(creator => creator.LoopCutMesh(_pointer, _size.Width, _size.Height, (int)Cuts, (float)Slide), "Point at a face with a ring of quads.");

    [RelayCommand]
    public void ApplyTransform() => Run(creator => _transform.Apply(creator), "Select points, edges or faces first.");

    /// <summary>Swings the selected door open or shut at once.</summary>
    [RelayCommand]
    public void Test()
    {
        if (_creator() is not { SelectedPart: { } id } creator || !creator.SwingDoor(id))
        {
            _report("Select a door part to test.");
        }
    }

    private void SetElement(bool on, MeshElement element)
    {
        if (on)
        {
            Element = element;
            Refresh();
        }
    }

    private void Run(Func<CreatorMode, bool> tool, string failure)
    {
        if (_creator() is not { } creator)
        {
            return;
        }

        if (!tool(creator))
        {
            _report(failure);
        }

        NotifyTransform();
    }

    private void SetTransform(Vector3 values)
    {
        _transform.Values = values;
        NotifyTransform();
    }

    private void NotifyTransform()
    {
        OnPropertyChanged(nameof(TransformX));
        OnPropertyChanged(nameof(TransformY));
        OnPropertyChanged(nameof(TransformZ));
        OnPropertyChanged(nameof(TransformTool));
        OnPropertyChanged(nameof(TransformToolIndex));
    }
}
