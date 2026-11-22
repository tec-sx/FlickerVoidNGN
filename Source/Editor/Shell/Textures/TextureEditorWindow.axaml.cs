using System.ComponentModel;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using FlickerVoid.Editor.Assets.Textures;

namespace FlickerVoid.Editor.Textures;

/// <summary>Shows a <see cref="TextureEditorViewModel"/>, copying its preview pixels into a bitmap.</summary>
public sealed partial class TextureEditorWindow : Window
{
    private readonly Image _preview;
    private TextureEditorViewModel? _viewModel;

    public TextureEditorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _preview = this.FindControl<Image>("Preview")!;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= HandleViewModelChanged;
        }

        _viewModel = DataContext as TextureEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += HandleViewModelChanged;
        }

        ShowPreview();
    }

    private void HandleViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextureEditorViewModel.PreviewPixels))
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

        int size = TextureEditorViewModel.PreviewSize;
        PixelSize pixelSize = new(size, size);
        Vector dpi = new(96, 96);
        WriteableBitmap bitmap = new(pixelSize, dpi, PixelFormats.Rgba8888, AlphaFormat.Unpremul);
        using (ILockedFramebuffer frame = bitmap.Lock())
        {
            int rowBytes = size * 4;
            for (int y = 0; y < size; y++)
            {
                nint row = frame.Address + (y * frame.RowBytes);
                Marshal.Copy(pixels, y * rowBytes, row, rowBytes);
            }
        }

        _preview.Source = bitmap;
    }
}
