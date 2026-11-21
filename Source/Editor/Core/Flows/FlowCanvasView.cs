using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Flows;

/// <summary>
/// Which part of the endless graph canvas the window shows: a canvas point appears at <c>point * Zoom + Offset</c> on screen.
/// It pans by screen pixels, zooms about a screen point, and frames a box of the canvas (F).
/// </summary>
public sealed partial class FlowCanvasView : ObservableObject
{
    public const double MinZoom = 0.2;

    public const double MaxZoom = 2;

    /// <summary>How much one wheel notch zooms.</summary>
    private const double ZoomPerStep = 1.15;

    /// <summary>The room left round a framed box, in screen pixels.</summary>
    private const double FrameMargin = 40;

    [ObservableProperty]
    public partial double OffsetX { get; private set; }

    [ObservableProperty]
    public partial double OffsetY { get; private set; }

    [ObservableProperty]
    public partial double Zoom { get; private set; } = 1;

    public FlowPoint ToCanvas(double screenX, double screenY)
    {
        return new FlowPoint((screenX - OffsetX) / Zoom, (screenY - OffsetY) / Zoom);
    }

    public FlowPoint ToScreen(double canvasX, double canvasY)
    {
        return new FlowPoint((canvasX * Zoom) + OffsetX, (canvasY * Zoom) + OffsetY);
    }

    public void Pan(double screenX, double screenY)
    {
        OffsetX += screenX;
        OffsetY += screenY;
    }

    /// <summary>Zooms in by whole or partial wheel steps, or out with negative ones, keeping the canvas point under the screen point still.</summary>
    public void ZoomAt(double screenX, double screenY, double steps)
    {
        FlowPoint anchor = ToCanvas(screenX, screenY);
        double zoom = Zoom * Math.Pow(ZoomPerStep, steps);
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        OffsetX = screenX - (anchor.X * Zoom);
        OffsetY = screenY - (anchor.Y * Zoom);
    }

    /// <summary>Shows the canvas box whole and centred in a view of the given size, zoomed in no further than 1.</summary>
    public void Frame(double minX, double minY, double maxX, double maxY, double width, double height)
    {
        double boxWidth = Math.Max(maxX - minX, 1);
        double boxHeight = Math.Max(maxY - minY, 1);
        double fitX = Math.Max(width - (2 * FrameMargin), 1) / boxWidth;
        double fitY = Math.Max(height - (2 * FrameMargin), 1) / boxHeight;
        double zoom = Math.Min(Math.Min(fitX, fitY), 1);
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        double centerX = (minX + maxX) / 2;
        double centerY = (minY + maxY) / 2;
        OffsetX = (width / 2) - (centerX * Zoom);
        OffsetY = (height / 2) - (centerY * Zoom);
    }
}
