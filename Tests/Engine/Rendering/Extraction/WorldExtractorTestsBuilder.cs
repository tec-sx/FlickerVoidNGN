using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Tests.Extraction;

/// <summary>Builds an Arch world with rendering components and extracts it into a render scene.</summary>
internal sealed class WorldExtractorTestsBuilder : IDisposable
{
    private readonly World _world = World.Create();
    private readonly RenderScene _scene = new();

    public WorldExtractorTestsBuilder WithCamera(Camera camera, Vector3 position)
    {
        Transform transform = new() { Position = position };
        _world.Create(camera, transform);
        return this;
    }

    public WorldExtractorTestsBuilder WithActiveCamera(Camera camera, Vector3 position, ActiveCamera active)
    {
        Transform transform = new() { Position = position };
        _world.Create(camera, transform, active);
        return this;
    }

    public WorldExtractorTestsBuilder WithMesh(string mesh, Transform transform)
    {
        MeshRenderer renderer = new() { Mesh = mesh };
        _world.Create(renderer, transform);
        return this;
    }

    public WorldExtractorTestsBuilder WithMesh(string mesh, Transform transform, Outline outline)
    {
        MeshRenderer renderer = new() { Mesh = mesh };
        _world.Create(renderer, transform, outline);
        return this;
    }

    public WorldExtractorTestsBuilder WithAnimatedMesh(string mesh, MeshAnimator animator, out int id)
    {
        MeshRenderer renderer = new() { Mesh = mesh };
        Entity entity = _world.Create(renderer, new Transform(), animator);
        id = entity.Id;
        return this;
    }

    public WorldExtractorTestsBuilder WithPointLight(PointLight light, Vector3 position)
    {
        Transform transform = new() { Position = position };
        _world.Create(light, transform);
        return this;
    }

    public WorldExtractorTestsBuilder WithSpotLight(SpotLight light, Transform transform)
    {
        _world.Create(light, transform);
        return this;
    }

    public WorldExtractorTestsBuilder WithVolume(IrradianceVolume volume, Vector3 center)
    {
        Transform transform = new() { Position = center };
        _world.Create(volume, transform);
        return this;
    }

    /// <summary>Extracts the world into the same scene each call, as the renderer does every frame.</summary>
    public RenderScene Extract()
    {
        WorldExtractor extractor = new();
        extractor.Extract(_world, _scene);
        return _scene;
    }

    public RenderCamera? FindCamera() => ActiveCamera.Find(_world);

    public void Dispose()
    {
        World.Destroy(_world);
    }
}
