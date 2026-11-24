using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

namespace FlickerVoid.Editor.Projects;

/// <summary>The splash shown over the project picker while a project builds; it closes with the picker once the editor opens.</summary>
public sealed partial class SplashWindow : Window
{
    private Bitmap? _image;

    public SplashWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SplashViewModel { Image: { } file } && LoadImage(file) is { } image)
        {
            _image = image;
            Image picture = this.GetControl<Image>("Picture");
            picture.Source = image;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _image?.Dispose();
    }

    /// <summary>The project's image, or none if it can't be read, leaving the star: a splash never keeps a project from opening.</summary>
    private static Bitmap? LoadImage(string file)
    {
        try
        {
            return new Bitmap(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
