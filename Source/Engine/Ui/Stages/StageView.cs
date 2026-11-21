using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FlickerVoid.Engine.Ui.Stages;

/// <summary>Shows a <see cref="Stage"/> at the control's size, shown only while the control is, and turns it as the pointer drags across.</summary>
public sealed class StageView : Control
{
    public static readonly StyledProperty<Stage?> StageProperty = AvaloniaProperty.Register<StageView, Stage?>(nameof(Stage));

    private Point? _dragFrom;

    static StageView()
    {
        AffectsRender<StageView>(StageProperty);
    }

    public StageView()
    {
        Background = Brushes.Transparent;
    }

    public Stage? Stage
    {
        get => GetValue(StageProperty);
        set => SetValue(StageProperty, value);
    }

    /// <summary>Drawn under the stage; transparent by default so the whole control takes the drag.</summary>
    public IBrush? Background { get; set; }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        context.FillRectangle(Background!, bounds);
        WriteableBitmap? bitmap = Stage?.Bitmap;
        if (bitmap is not null)
        {
            Rect source = new(bitmap.Size);
            context.DrawImage(bitmap, source, bounds);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StageProperty)
        {
            if (change.OldValue is Stage old)
            {
                old.Rendered -= InvalidateVisual;
                old.IsShown = false;
            }

            if (change.NewValue is Stage stage)
            {
                stage.Rendered += InvalidateVisual;
            }

            UpdateStage();
        }
        else if (change.Property == BoundsProperty || change.Property == IsVisibleProperty)
        {
            UpdateStage();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateStage();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateStage();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragFrom = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragFrom is not { } from || Stage is null)
        {
            return;
        }

        Point to = e.GetPosition(this);
        double pixels = to.X - from.X;
        Stage.Drag((float)pixels);
        _dragFrom = to;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragFrom = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragFrom = null;
    }

    /// <summary>Shows the stage while the control is on screen, at the control's size in pixels.</summary>
    private void UpdateStage()
    {
        Stage? stage = Stage;
        if (stage is null)
        {
            return;
        }

        TopLevel? top = TopLevel.GetTopLevel(this);
        double scaling = top?.RenderScaling ?? 1;
        PixelSize size = PixelSize.FromSize(Bounds.Size, scaling);
        stage.Size = size;
        stage.IsShown = top is not null && IsEffectivelyVisible && size.Width > 0 && size.Height > 0;
    }
}
