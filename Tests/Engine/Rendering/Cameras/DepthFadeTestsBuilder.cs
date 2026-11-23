using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Tests.Cameras;

/// <summary>Builds a world of cameras, one of them perhaps the main camera, and finds the depth fade each draws with.</summary>
internal sealed class DepthFadeTestsBuilder : IDisposable
{
    private readonly World _world = World.Create();

    public Entity WithMainCamera(MainCamera main)
    {
        return _world.Create(new Camera(), new Transform(), main);
    }

    public Entity WithCamera()
    {
        return _world.Create(new Camera(), new Transform());
    }

    public Entity WithCamera(DepthFadeOverride change)
    {
        return _world.Create(new Camera(), new Transform(), change);
    }

    public DepthFade FindFor(Entity camera) => MainCamera.FindFor(_world, camera);

    public void Dispose()
    {
        World.Destroy(_world);
    }
}
