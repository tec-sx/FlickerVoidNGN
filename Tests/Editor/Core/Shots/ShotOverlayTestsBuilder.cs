using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Shots;

namespace FlickerVoid.Editor.Core.Tests.Shots;

/// <summary>Builds the scene and timeline of <see cref="ShotTimelineViewModelTestsBuilder"/> and draws the shot overlay seen from above the origin.</summary>
internal sealed class ShotOverlayTestsBuilder : IDisposable
{
    public ShotTimelineViewModelTestsBuilder Scene { get; } = new();

    public OverlayBuilder Draw()
    {
        EditorCamera camera = new();
        camera.LookAt(Vector3.Zero, 0, -1, 30);
        OverlayBuilder overlay = new();
        overlay.Begin(camera, 480);
        ShotOverlay.Draw(overlay, Scene.Scene.Game.World, Scene.Timeline);
        return overlay;
    }

    public void Dispose() => Scene.Dispose();
}
