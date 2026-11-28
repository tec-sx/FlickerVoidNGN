using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Viewports;

namespace FlickerVoid.Editor.Viewports;

/// <summary>
/// The controls drawn over the viewport: Unreal-style tool and view icons in the top left, and Blender's navigation gizmo with its
/// pan and zoom handles in the top right, and the name chips of the scene's music areas.
/// </summary>
public sealed partial class ViewportOverlay : UserControl
{
    private readonly DispatcherTimer _chips = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public ViewportOverlay()
    {
        AvaloniaXamlLoader.Load(this);
        _chips.Tick += (_, _) => RefreshChips();
        AttachedToVisualTree += (_, _) => _chips.Start();
        DetachedFromVisualTree += (_, _) => _chips.Stop();
    }

    private void RefreshChips()
    {
        Canvas? canvas = this.FindControl<Canvas>("AreaChips");
        if (canvas is null)
        {
            return;
        }

        canvas.Children.Clear();
        if (DataContext is not ViewportViewModel { IsGameView: false } viewModel || Bounds.Width < 1 || Bounds.Height < 1)
        {
            return;
        }

        IReadOnlyList<MusicAreaLabel> labels = MusicAreaLabels.Collect(viewModel.Scene.Game.World, viewModel.Camera, (int)Bounds.Width, (int)Bounds.Height);
        foreach (MusicAreaLabel label in labels)
        {
            StackPanel text = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
            text.Children.Add(new TextBlock { Text = label.Name, FontWeight = FontWeight.SemiBold, FontSize = 12 });
            if (label.Layers.Length > 0)
            {
                text.Children.Add(new TextBlock { Text = label.Layers, FontSize = 11, Opacity = 0.7 });
            }

            Border chip = new() { Child = text };
            chip.Classes.Add("areaChip");
            Canvas.SetLeft(chip, label.X - 40);
            Canvas.SetTop(chip, label.Y - 24);
            canvas.Children.Add(chip);
        }
    }
}
