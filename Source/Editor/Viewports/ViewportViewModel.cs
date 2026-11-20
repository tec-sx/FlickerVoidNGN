using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Editor.Cameras;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>The scene view: the world it shows, the editor camera looking at it, and what the viewport reports.</summary>
public sealed partial class ViewportViewModel : ObservableObject
{
    private readonly EditorCameraController _controller;

    public ViewportViewModel(EditorScene scene, bool validation)
    {
        Scene = scene;
        Validation = validation;
        Camera.LookAt(Vector3.Zero, MathF.PI / 4, -0.5f, 12);
        _controller = new EditorCameraController(Camera);
    }

    public EditorScene Scene { get; }

    public EditorCamera Camera { get; } = new();

    /// <summary>Whether the viewport's renderer turns on the Vulkan validation layer.</summary>
    public bool Validation { get; }

    /// <summary>What the viewport shows on, or why it can't.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "Starting the viewport.";

    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    public bool IsPerspective
    {
        get => Camera.Projection == CameraProjection.Perspective;
        set
        {
            CameraProjection projection = CameraProjection.Orthographic;
            if (value)
            {
                projection = CameraProjection.Perspective;
            }

            if (projection == Camera.Projection)
            {
                return;
            }

            Camera.Projection = projection;
            OnPropertyChanged();
        }
    }

    /// <summary>Moves the camera by one frame of input over a viewport the given number of pixels high.</summary>
    public void Update(in ViewportInput input, float seconds, int viewportHeight)
    {
        _controller.Apply(input, seconds, viewportHeight);
    }
}
