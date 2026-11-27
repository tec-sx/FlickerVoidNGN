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
    private bool _dragTyped;
    private bool _dragWrites;

    public MeshToolsViewModel(Func<CreatorMode?> creator, Action<string> report)
    {
        _creator = creator;
        _report = report;
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
    public partial double Angle { get; set; } = 90;

    [ObservableProperty]
    public partial bool Round { get; set; }

    [ObservableProperty]
    public partial bool Through { get; set; }

    [ObservableProperty]
    public partial bool Outward { get; set; } = true;

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

    public void Refresh()
    {
        OnPropertyChanged(nameof(IsCreating));
        OnPropertyChanged(nameof(IsDragging));
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
        if (_dragTyped || _creator() is not { MeshDrag: { } tool } creator)
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
    /// Starts a tool whose amount the mouse sets (E, I, C, Ctrl+B): moving it up makes more, a click keeps it, a right-click or Escape
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
        _dragTyped = false;
        _report($"{tool}: move the mouse up or down, click to keep, right-click or Escape to cancel.");
        OnPropertyChanged(nameof(IsDragging));
    }

    /// <summary>Keeps what the dragged tool shows; false when no tool is dragged.</summary>
    public bool ConfirmDrag() => EndDrag(keep: true);

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

    /// <summary>The right-click menu of the mesh: the tools, then the switches with what each is set to.</summary>
    public IReadOnlyList<MenuChoice> GetMenu()
    {
        const string Tools = "Tools";
        const string Doors = "Doors";
        const string Switches = "Switches";
        return
        [
            new MenuChoice("Extrude", DragExtrude, Tools, null, "E"),
            new MenuChoice("Inset", DragInset, Tools, null, "I"),
            new MenuChoice("Cut", DragCut, Tools, null, "C"),
            new MenuChoice("Bevel", DragBevel, Tools, null, "Ctrl+B"),
            new MenuChoice("Loop Cut", LoopCut, Tools, null, "Ctrl+R"),
            new MenuChoice("Hang Door", HangDoor, Doors, null, "D"),
            new MenuChoice("Test Door", Test, Doors),
            new MenuChoice(Describe("Mirror", Mirror), () => Mirror = !Mirror, Switches),
            new MenuChoice(Describe("Round Bevels", Round), () => Round = !Round, Switches),
            new MenuChoice(Describe("Cut Through", Through), () => Through = !Through, Switches),
            new MenuChoice(Describe("Doors Swing Out", Outward), () => Outward = !Outward, Switches),
        ];
    }

    partial void OnAmountChanged(double value) => TypeDragAmount(value, thickness: false);

    partial void OnThicknessChanged(double value) => TypeDragAmount(value, thickness: true);

    private static string Describe(string name, bool on) => on ? $"{name}: On" : $"{name}: Off";

    private static bool UsesThickness(MeshDragTool tool) => tool is MeshDragTool.Inset or MeshDragTool.Bevel;

    // A number typed while its tool is dragged sets the amount, and the mouse lets go of it.
    private void TypeDragAmount(double value, bool thickness)
    {
        if (_dragWrites || _creator() is not { MeshDrag: { } tool } creator || UsesThickness(tool) != thickness)
        {
            return;
        }

        _dragTyped = true;
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

    [RelayCommand]
    public void HangDoor()
    {
        if (_creator() is not { } creator)
        {
            return;
        }

        DoorOptions options = new() { Thickness = (float)Thickness, Angle = (float)Angle, Outward = Outward, Through = Through };
        string? door = creator.HangDoor(_pointer, _size.Width, _size.Height, options);
        if (door is null)
        {
            _report("Point at a flat face to hang the door in.");
            return;
        }

        _report($"Hung {door}; press Test to swing it.");
    }

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
