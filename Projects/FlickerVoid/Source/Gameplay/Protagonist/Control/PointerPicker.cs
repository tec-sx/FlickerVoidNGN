using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Geometry;

using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Protagonist.Control;

using World = Arch.Core.World;

/// <summary>Finds what the mouse points at: a ray from the drawn camera (the director's posed view, else the first camera) through the mouse, met by the physics world. A game service.</summary>
public sealed class PointerPicker(World world, IInputState input, IGameWindow window, IPhysicsSystem physics)
{
    private const float MaxDistance = 500;

    public bool TryPick(out RayHit hit)
    {
        hit = default;
        RenderCamera? camera = ActiveCamera.Find(world);
        if (camera is not { } drawn || window.Size.X <= 0 || window.Size.Y <= 0)
        {
            return false;
        }

        CameraView view = CameraView.Create(drawn.Camera, drawn.Transform, window.Size.X, window.Size.Y);
        Ray ray = Ray.FromViewProjection(view.ViewProjection, input.MousePosition, window.Size.X, window.Size.Y);

        return physics.RayCast(ray, MaxDistance, out hit);
    }
}
