using System.ComponentModel;
using System.Numerics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using FlickerVoid.Editor.Assets.Materials;

namespace FlickerVoid.Editor.Materials;

/// <summary>Shows a <see cref="MaterialEditorViewModel"/>: copies its preview pixels into a bitmap and turns the preview by drags.</summary>
public sealed partial class MaterialEditorWindow : Window
{
    private readonly Image _preview;
    private readonly Border _previewArea;
    private MaterialEditorViewModel? _viewModel;
    private Point? _lastPointer;
    private bool _turningLight;

    public MaterialEditorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _preview = this.FindControl<Image>("Preview")!;
        _previewArea = this.FindControl<Border>("PreviewArea")!;
        _previewArea.SizeChanged += HandlePreviewAreaResized;
        _preview.PointerPressed += HandlePreviewPressed;
        _preview.PointerMoved += HandlePreviewMoved;
        _preview.PointerReleased += HandlePreviewReleased;
        UndoShortcuts.Attach(this, () => _viewModel?.History);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= HandleViewModelChanged;
        }

        _viewModel = DataContext as MaterialEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += HandleViewModelChanged;
            Size area = _previewArea.Bounds.Size;
            if (area.Width > 0)
            {
                double side = Math.Min(area.Width, area.Height) * RenderScaling;
                _viewModel.ResizePreview((int)side);
            }
        }

        ShowPreview();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel?.Dispose();
    }

    private void HandleViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MaterialEditorViewModel.PreviewPixels))
        {
            ShowPreview();
        }
    }

    private void ShowPreview()
    {
        byte[]? pixels = _viewModel?.PreviewPixels;
        if (pixels is null)
        {
            _preview.Source = null;
            return;
        }

        int size = (int)Math.Sqrt(pixels.Length / 4);
        PixelSize pixelSize = new(size, size);
        Avalonia.Vector dpi = new(96, 96);
        WriteableBitmap bitmap = new(pixelSize, dpi, PixelFormats.Rgba8888, AlphaFormat.Opaque);
        using (ILockedFramebuffer frame = bitmap.Lock())
        {
            int rowBytes = size * 4;
            for (int y = 0; y < size; y++)
            {
                nint row = frame.Address + (y * frame.RowBytes);
                System.Runtime.InteropServices.Marshal.Copy(pixels, y * rowBytes, row, rowBytes);
            }
        }

        _preview.Source = bitmap;
    }

    /// <summary>Renders the preview at the size of its area, in device pixels, so it stays sharp as the window or the splitters change it.</summary>
    private void HandlePreviewAreaResized(object? sender, SizeChangedEventArgs e)
    {
        double side = Math.Min(e.NewSize.Width, e.NewSize.Height) * RenderScaling;
        _viewModel?.ResizePreview((int)side);
    }

    private void HandlePreviewPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastPointer = e.GetPosition(_preview);
        PointerPointProperties pressed = e.GetCurrentPoint(_preview).Properties;
        _turningLight = pressed.IsRightButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        e.Pointer.Capture(_preview);
    }

    private void HandlePreviewMoved(object? sender, PointerEventArgs e)
    {
        if (_lastPointer is not { } last || _viewModel is null)
        {
            return;
        }

        Point position = e.GetPosition(_preview);
        Point moved = position - last;
        _lastPointer = position;
        Vector2 drag = new((float)moved.X, (float)moved.Y);
        if (_turningLight)
        {
            _viewModel.DragLight(drag);
            return;
        }

        _viewModel.DragPreview(drag);
    }

    private void HandlePreviewReleased(object? sender, PointerReleasedEventArgs e)
    {
        _lastPointer = null;
        e.Pointer.Capture(null);
    }
}
