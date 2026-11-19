using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds scenes on a <see cref="SceneRendererTestsBuilder"/> and bakes a volume holding the scene, with a single probe one unit above the
/// origin, where a 10 by 10 floor lies facing up, lit straight down by the sun with no ambient light.
/// </summary>
internal sealed class IrradianceBakerTestsBuilder : IDisposable
{
    private static readonly Vector4 Red = new(1, 0, 0, 1);

    private readonly SceneRendererTestsBuilder _scene = new();
    private readonly List<string> _entities = [];
    private bool _modelWritten;
    private bool _sunShadows = true;

    public IrradianceBakerTestsBuilder WithSunlitRedFloor()
    {
        _entities.Add("""{ "name": "Floor", "components": { "transform": { "rotation": [-0.70710677, 0, 0, 0.70710677], "scale": [5, 5, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        return this;
    }

    /// <summary>
    /// A ceiling facing down two units up. The sun shines through it (no sun shadows), but can't light its underside, which
    /// shows only light bounced off the floor.
    /// </summary>
    public IrradianceBakerTestsBuilder WithRedCeiling()
    {
        _sunShadows = false;
        _entities.Add("""{ "name": "Ceiling", "components": { "transform": { "position": [0, 2, 0], "rotation": [0.70710677, 0, 0, 0.70710677], "scale": [5, 5, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        return this;
    }

    public IrradianceProbe BakeOneProbeAboveTheFloor(int bounces)
    {
        if (!_modelWritten)
        {
            _scene.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
            _scene.WithScene([.. _entities]);
            _scene.WithLight(sunDirection: -Vector3.UnitY, ambient: 0);
            _modelWritten = true;
        }

        IrradianceVolume volume = new() { Size = new Vector3(10, 3, 10), ProbesX = 1, ProbesY = 1, ProbesZ = 1 };
        using IrradianceBaker baker = _scene.BuildBaker();
        if (!_sunShadows)
        {
            baker.Renderer.Mood = baker.Renderer.Mood with { SunShadowDistance = 0 };
        }

        IrradianceProbes probes = baker.Bake(_scene.World, volume, Vector3.UnitY, bounces);
        return probes.Probes[0];
    }

    public void Dispose() => _scene.Dispose();
}
