using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Meshes.Doors;

namespace FlickerVoid.Editor.Viewports;

/// <summary>The tool bar of the mesh mode: the element kind, the tools with their numbers, the mirror switch and the door test.</summary>
public sealed partial class MeshToolsViewModel : ObservableObject
{
    private readonly Func<CreatorMode?> _creator;
    private readonly Action<string> _report;
    private readonly MeshTransformBar _transform = new();
    private Vector2 _pointer;
    private (int Width, int Height) _size = (1, 1);

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

    public bool IsMeshMode => _creator()?.MeshSelection is not null;

    public MeshElement Element
    {
        get => _creator()?.MeshSelection?.Element ?? MeshElement.Point;
        set
        {
            _creator()?.MeshSelection?.SetElement(value);
            OnPropertyChanged();
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
        OnPropertyChanged(nameof(IsMeshMode));
        OnPropertyChanged(nameof(Element));
        OnPropertyChanged(nameof(IsPoint));
        OnPropertyChanged(nameof(IsEdge));
        OnPropertyChanged(nameof(IsFace));
        OnPropertyChanged(nameof(Mirror));
    }

    /// <summary>Remembers where the mouse is, for the tools that act on what is under it.</summary>
    public void Point(Vector2 pixel, int width, int height)
    {
        _pointer = pixel;
        _size = (width, height);
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
            _report("Select a part with a mesh first.");
        }
    }

    [RelayCommand]
    public void Leave() => _creator()?.LeaveMesh();

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
