using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Materials;

/// <summary>
/// The material editor window: a browser of the content folder's materials, a field for each parameter the scene shader
/// declares, held unsaved as it changes until saved or discarded, and a preview of the material on a sphere or a cube.
/// </summary>
public sealed partial class MaterialEditorViewModel : ObservableObject, IDisposable
{
    public const int PreviewSize = 256;

    private const string NewMaterialName = "New Material";
    private const float RadiansPerPixel = 0.01f;

    private readonly UnsavedContent _content;
    private readonly IMaterialPreview? _preview;
    private readonly ContentFileFinder _files;
    private readonly MaterialBrowser _browser;
    private readonly IReadOnlyList<ShaderParameter> _declared;
    private MaterialDocument? _document;

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
        FindMaterials();
    }

    /// <summary>Narrows the browser to materials whose path holds it.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> Materials { get; set; } = [];

    [ObservableProperty]
    public partial string? SelectedMaterial { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<FieldViewModel> Parameters { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSphere))]
    public partial bool IsCube { get; set; }

    public bool IsSphere
    {
        get => !IsCube;
        set => IsCube = !value;
    }

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
        _content.UnsavedChanged -= HandleUnsavedChanged;
        if (_document is not null)
        {
            _document.Changed -= RenderPreview;
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

    partial void OnSearchTextChanged(string value) => FindMaterials();

    partial void OnSelectedMaterialChanged(string? value)
    {
        Open(value);
        HandleUnsavedChanged();
    }

    partial void OnIsCubeChanged(bool value) => RenderPreview();

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

    private void FindMaterials()
    {
        Materials = _browser.Find(SearchText);
    }

    private void Open(string? path)
    {
        if (_document is not null)
        {
            _document.Changed -= RenderPreview;
            _document = null;
        }

        Parameters = [];
        Status = "";
        if (path is not null)
        {
            try
            {
                _document = new MaterialDocument(_content, path);
            }
            catch (InvalidDataException exception)
            {
                Status = exception.Message;
            }
        }

        if (_document is null)
        {
            RenderPreview();
            return;
        }

        _document.Changed += RenderPreview;
        List<FieldViewModel> fields = [];
        foreach (ShaderParameter parameter in _declared)
        {
            MaterialParameterValue value = new(_document, parameter);
            FieldViewModel field = FieldFactory.Create(value, _files.Find);
            fields.Add(field);
        }

        Parameters = fields;
        RenderPreview();
    }

    private void RenderPreview()
    {
        if (_preview is null || _document is null)
        {
            PreviewPixels = null;
            return;
        }

        PreviewShape shape = PreviewShape.Sphere;
        if (IsCube)
        {
            shape = PreviewShape.Cube;
        }

        try
        {
            PreviewPixels = _preview.Render(_document.Material, shape, PreviewYaw, PreviewSize);
        }
        catch (InvalidDataException exception)
        {
            Status = exception.Message;
        }
    }
}
