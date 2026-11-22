using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Protagonist.Control;

using World = Arch.Core.World;

/// <summary>Finds what the mouse points at: a ray from the first camera through the mouse, met by the physics world. A game service.</summary>
public sealed class PointerPicker(World world, IInputState input, IGameWindow window, IPhysicsSystem physics)
{
    private const float MaxDistance = 500;

    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();

    public bool TryPick(out RayHit hit)
    {
        hit = default;
        Entity camera = Entity.Null;
        world.Query(in Cameras, (Entity entity) =>
        {
            if (camera == Entity.Null)
            {
                camera = entity;
            }
        });
        Vector2D<int> size = window.Size;
        if (camera == Entity.Null || size.X <= 0 || size.Y <= 0)
        {
            return false;
        }

        Camera lens = world.Get<Camera>(camera);
        Transform place = world.Get<Transform>(camera);
        CameraView view = CameraView.Create(lens, place, size.X, size.Y);
        Ray ray = view.GetRay(input.MousePosition, size.X, size.Y);
        return physics.RayCast(ray, MaxDistance, out hit);
    }
}
