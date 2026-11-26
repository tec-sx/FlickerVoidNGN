using System.Numerics;
using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Materials;

/// <summary>
/// The material editor window: a browser of the content folder's materials nested under the masters and materials they are
/// instances of, the open material's parent and a row for each parameter the scene shader and its master declare (showing its
/// parent's value until it overrides it), held unsaved as it changes until saved or discarded, and a preview of the material on
/// a sphere, a cube or a plane, by day, at night or indoors, its light turned by a drag.
/// </summary>
public sealed partial class MaterialEditorViewModel : ObservableObject, IDisposable
{
    public const int PreviewSize = 256;

    private const string NewMaterialName = "New Material";
    private const string InstanceSuffix = " Instance";
    private const float RadiansPerPixel = 0.01f;

    private readonly UnsavedContent _content;
    private readonly IMaterialPreview? _preview;
    private readonly ContentFileFinder _files;
    private readonly MaterialBrowser _browser;
    private readonly IReadOnlyList<ShaderParameter> _declared;
    private MaterialDocument? _document;
    private string? _openParent;
    private bool _selectingNode;

    /// <summary>Without a preview (no GPU), the window edits materials without showing them.</summary>
    public MaterialEditorViewModel(UnsavedContent content, IMaterialPreview? preview)
    {
        _content = content;
        _preview = preview;
        _browser = new MaterialBrowser(content);
        _files = new ContentFileFinder(content);
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Rendering");
        _declared = ShaderParameters.Load(shaders, "Scene").Parameters;
        _content.Changed += HandleContentChanged;
        _content.UnsavedChanged += HandleUnsavedChanged;
        _content.Restored += HandleRestored;
        FindMaterials();
    }

    /// <summary>Narrows the browser to materials whose path holds it.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> Materials { get; set; } = [];

    /// <summary>The materials as a tree under the masters and materials they are instances of.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MaterialTreeNode> Tree { get; set; } = [];

    /// <summary>The master or material picked in the tree; a material opens, a master can only have instances made of it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateInstanceCommand))]
    public partial MaterialTreeNode? SelectedNode { get; set; }

    [ObservableProperty]
    public partial string? SelectedMaterial { get; set; }

    /// <summary>What the master picked in the tree is: its shading model and its own parameters; empty unless a master is picked.</summary>
    [ObservableProperty]
    public partial string MasterSummary { get; set; } = "";

    /// <summary>The open material's parent, a master or material it is an instance of.</summary>
    [ObservableProperty]
    public partial FieldViewModel? Parent { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MaterialParameterViewModel> Parameters { get; set; } = [];

    /// <summary>The parameters' fields.</summary>
    public IEnumerable<FieldViewModel> Fields => Parameters.Select(static parameter => parameter.Field);

    /// <summary>The shape the preview shows the material on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSphere), nameof(IsCube), nameof(IsPlane))]
    public partial PreviewShape Shape { get; set; }

    public bool IsSphere
    {
        get => Shape == PreviewShape.Sphere;
        set => PickShape(PreviewShape.Sphere, value);
    }

    public bool IsCube
    {
        get => Shape == PreviewShape.Cube;
        set => PickShape(PreviewShape.Cube, value);
    }

    public bool IsPlane
    {
        get => Shape == PreviewShape.Plane;
        set => PickShape(PreviewShape.Plane, value);
    }

    /// <summary>How the preview is lit: by day, at night by a lamp, or indoors.</summary>
    [ObservableProperty]
    public partial PreviewMood Mood { get; set; }

    /// <summary>The moods the preview can be lit in, for the window's picker.</summary>
    public IReadOnlyList<PreviewMood> Moods { get; } = Enum.GetValues<PreviewMood>();

    /// <summary>How far the preview's sun or lamp is turned about the shape, in radians.</summary>
    [ObservableProperty]
    public partial float LightYaw { get; set; }

    /// <summary>How far the preview shape is turned about its vertical axis, in radians.</summary>
    [ObservableProperty]
    public partial float PreviewYaw { get; set; } = 0.6f;

    /// <summary>The preview as Rgba8 pixels, <see cref="PreviewSize"/> square, or null with nothing to show.</summary>
    [ObservableProperty]
    public partial byte[]? PreviewPixels { get; set; }

    /// <summary>Why a material can't be shown or edited, if it can't.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "";

    /// <summary>Whether the open material has edits that are not saved.</summary>
    /// <summary>The open material's undo and redo steps, or null with none open.</summary>
    public UndoHistory? History
    {
        get
        {
            if (SelectedMaterial is null)
            {
                return null;
            }

            return _content.GetHistory(SelectedMaterial);
        }
    }

    public bool IsUnsaved => SelectedMaterial is not null && _content.IsUnsaved(SelectedMaterial);

    /// <summary>The open material's path, marked with an asterisk when it has unsaved edits.</summary>
    public string Heading
    {
        get
        {
            if (SelectedMaterial is null)
            {
                return "";
            }

            if (IsUnsaved)
            {
                return $"{SelectedMaterial} *";
            }

            return SelectedMaterial;
        }
    }

    /// <summary>Releases the preview's GPU device when it owns one.</summary>
    public void Dispose()
    {
        _content.Changed -= HandleContentChanged;
        _content.Restored -= HandleRestored;
        _content.UnsavedChanged -= HandleUnsavedChanged;
        if (_document is not null)
        {
            _document.Changed -= HandleDocumentChanged;
        }

        if (_preview is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>Turns the preview shape by a mouse drag in pixels.</summary>
    public void DragPreview(Vector2 pixels)
    {
        PreviewYaw += pixels.X * RadiansPerPixel;
        RenderPreview();
    }

    /// <summary>Turns the preview's sun or lamp about the shape by a mouse drag in pixels.</summary>
    public void DragLight(Vector2 pixels)
    {
        LightYaw += pixels.X * RadiansPerPixel;
        RenderPreview();
    }

    [RelayCommand(CanExecute = nameof(IsUnsaved))]
    private void Save()
    {
        _content.Save(SelectedMaterial!);
    }

    /// <summary>Forgets the open material's unsaved edits and shows it as saved.</summary>
    [RelayCommand(CanExecute = nameof(IsUnsaved))]
    private void Discard()
    {
        _content.Discard(SelectedMaterial!);
    }

    [RelayCommand]
    private void CreateMaterial()
    {
        string path = _browser.Create(NewMaterialName);
        SearchText = "";
        FindMaterials();
        SelectedMaterial = path;
    }

    /// <summary>Makes an instance of the master or material picked, keeping all its values, and opens it.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedNode))]
    private void CreateInstance()
    {
        MaterialTreeNode parent = SelectedNode!;
        string path = _browser.Create(parent.Name + InstanceSuffix, parent.Path);
        SearchText = "";
        FindMaterials();
        SelectedMaterial = path;
    }

    private bool HasSelectedNode() => SelectedNode is not null;

    partial void OnSearchTextChanged(string value) => FindMaterials();

    partial void OnSelectedNodeChanged(MaterialTreeNode? value)
    {
        if (_selectingNode)
        {
            return;
        }

        MasterSummary = "";
        if (value is null || value.IsMaster)
        {
            SelectedMaterial = null;
            if (value is not null)
            {
                MasterSummary = DescribeMaster(value.Path);
            }

            return;
        }

        SelectedMaterial = value.Path;
    }

    partial void OnSelectedMaterialChanged(string? value)
    {
        _selectingNode = true;
        try
        {
            if (value is not null)
            {
                SelectedNode = FindNode(Tree, value);
            }
            else if (SelectedNode is { IsMaster: false })
            {
                SelectedNode = null;
            }
        }
        finally
        {
            _selectingNode = false;
        }

        Open(value);
        HandleUnsavedChanged();
    }

    partial void OnShapeChanged(PreviewShape value) => RenderPreview();

    partial void OnMoodChanged(PreviewMood value) => RenderPreview();

    /// <summary>Picks the shape when its toggle is turned on; turning the shown shape's toggle off keeps it.</summary>
    private void PickShape(PreviewShape shape, bool picked)
    {
        if (picked)
        {
            Shape = shape;
            return;
        }

        OnPropertyChanged(nameof(IsSphere));
        OnPropertyChanged(nameof(IsCube));
        OnPropertyChanged(nameof(IsPlane));
    }

    private void HandleUnsavedChanged()
    {
        OnPropertyChanged(nameof(IsUnsaved));
        OnPropertyChanged(nameof(Heading));
        SaveCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Shows the open material again when its file changed other than by an edit here: discarded or changed on disk.</summary>
    private void HandleContentChanged(string fullPath)
    {
        if (SelectedMaterial is null || _content.IsUnsaved(SelectedMaterial))
        {
            return;
        }

        string open = _content.GetFullPath(SelectedMaterial);
        if (ContentManager.PathComparer.Equals(open, fullPath))
        {
            Open(SelectedMaterial);
        }
    }

    /// <summary>Shows the open material again after an undo or redo changed it.</summary>
    private void HandleRestored(string fullPath)
    {
        if (SelectedMaterial is null)
        {
            return;
        }

        string open = _content.GetFullPath(SelectedMaterial);
        if (ContentManager.PathComparer.Equals(open, fullPath))
        {
            Open(SelectedMaterial);
        }
    }

    private void FindMaterials()
    {
        Materials = _browser.Find(SearchText);
        Tree = _browser.FindTree(SearchText);
    }

    private string DescribeMaster(string path)
    {
        try
        {
            MasterFile master = _content.Load(path, RenderingJsonContext.Default.MasterFile);
            IEnumerable<string> names = master.Parameters.Select(static parameter => DisplayNames.FromIdentifier(parameter.Name));
            string parameters = string.Join(", ", names);
            if (parameters.Length == 0)
            {
                parameters = "none of its own";
            }

            return $"Master {path}: {master.ShadingModel} shading. Parameters: {parameters}. New instance makes a material of it.";
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException)
        {
            return $"Master {path} can't be read: {exception.Message}";
        }
    }

    private static MaterialTreeNode? FindNode(IReadOnlyList<MaterialTreeNode> nodes, string path)
    {
        foreach (MaterialTreeNode node in nodes)
        {
            if (ContentManager.PathComparer.Equals(node.Path, path))
            {
                return node;
            }

            MaterialTreeNode? found = FindNode(node.Children, path);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Opens the material again when its parent changed, as its parameters and inherited values do; otherwise shows the edit.</summary>
    private void HandleDocumentChanged()
    {
        if (_document is null)
        {
            return;
        }

        if (_document.Material.Parent != _openParent)
        {
            Open(SelectedMaterial);
            FindMaterials();
            return;
        }

        foreach (MaterialParameterViewModel parameter in Parameters)
        {
            parameter.Refresh();
        }

        RenderPreview();
    }

    private void Open(string? path)
    {
        if (_document is not null)
        {
            _document.Changed -= HandleDocumentChanged;
            _document = null;
        }

        Parameters = [];
        Parent = null;
        Status = "";
        MaterialLineage? lineage = null;
        if (path is not null)
        {
            try
            {
                _document = new MaterialDocument(_content, path);
                lineage = MaterialLineage.Of(_document.Material, _content);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException)
            {
                Status = exception.Message;
            }
        }

        if (_document is null)
        {
            RenderPreview();
            return;
        }

        _document.Changed += HandleDocumentChanged;
        _openParent = _document.Material.Parent;
        MaterialParentValue parent = new(_document);
        Parent = FieldFactory.Create(parent, _files.Find);
        if (lineage is null)
        {
            RenderPreview();
            return;
        }

        List<MaterialParameterViewModel> rows = [];
        IReadOnlyList<ShaderParameter> parameters = lineage.GetParameters(_declared);
        foreach (ShaderParameter parameter in parameters)
        {
            InheritedValue? inherited = lineage.Inherited.GetValueOrDefault(parameter.Name);
            MaterialParameterValue value = new(_document, parameter, inherited);
            FieldViewModel field = FieldFactory.Create(value, _files.Find);
            rows.Add(new MaterialParameterViewModel(value, field));
        }

        Parameters = rows;
        RenderPreview();
    }

    private void RenderPreview()
    {
        if (_preview is null || _document is null)
        {
            PreviewPixels = null;
            return;
        }

        PreviewView view = new(Shape, PreviewYaw, Mood, LightYaw);
        try
        {
            PreviewPixels = _preview.Render(_document.Material, view, PreviewSize);
        }
        catch (InvalidDataException exception)
        {
            Status = exception.Message;
        }
    }
}
