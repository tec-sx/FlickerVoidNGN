using System.Numerics;

using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds scenes on a <see cref="SceneRendererTestsBuilder"/> and bakes a volume holding the scene, with a single probe one unit above the
/// origin, where a 10 by 10 floor lies facing up, lit straight down by the sun with no ambient light; skips the test where the device
/// has no ray queries, which baking traces.
/// </summary>
internal sealed class IrradianceBakerTestsBuilder : IDisposable
{
    private static readonly Vector4 Red = new(1, 0, 0, 1);

    private readonly SceneRendererTestsBuilder _scene = new();
    private readonly List<string> _entities = [];
    private bool _modelWritten;

    public IrradianceBakerTestsBuilder WithSunlitRedFloor()
    {
        _entities.Add("""{ "name": "Floor", "components": { "transform": { "rotation": [-0.70710677, 0, 0, 0.70710677], "scale": [5, 5, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        return this;
    }

    /// <summary>The floor glowing red, so it lights what is above it in the dark.</summary>
    public IrradianceBakerTestsBuilder WithGlowingRedFloor()
    {
        _scene.WithFile("Glow.fvmat", """{ "parameters": { "emissive": [1, 0, 0] } }""");
        _entities.Add("""{ "name": "Floor", "components": { "transform": { "rotation": [-0.70710677, 0, 0, 0.70710677], "scale": [5, 5, 1] }, "meshRenderer": { "mesh": "Quad.glb", "materials": ["Glow.fvmat"] } } }""");
        return this;
    }

    /// <summary>A ceiling facing down two units up, which hides the floor from the sun and shows only light bounced off the floor.</summary>
    public IrradianceBakerTestsBuilder WithRedCeiling()
    {
        _entities.Add("""{ "name": "Ceiling", "components": { "transform": { "position": [0, 2, 0], "rotation": [0.70710677, 0, 0, 0.70710677], "scale": [5, 5, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        return this;
    }

    public IrradianceProbe BakeOneProbeAboveTheFloor(int bounces)
    {
        IrradianceProbes probes = BakeProbesAboveTheFloor(bounces);
        return probes.Probes[0];
    }

    public IrradianceProbes BakeProbesAboveTheFloor(int bounces)
    {
        if (!_modelWritten)
        {
            _scene.WithRayQueries();
            _scene.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
            _scene.WithScene([.. _entities]);
            _scene.WithLight(sunDirection: -Vector3.UnitY, ambient: 0);
            _modelWritten = true;
        }

        IrradianceVolume volume = new() { Size = new Vector3(10, 3, 10), ProbesX = 1, ProbesY = 1, ProbesZ = 1 };
        using IrradianceBaker baker = _scene.BuildBaker();
        return baker.Bake(_scene.World, volume, Vector3.UnitY, bounces);
    }

    /// <summary>The first probe's mean distance and mean square distance in the texel of its map round the direction.</summary>
    public static Vector2 DistanceToward(IrradianceProbes probes, Vector3 direction)
    {
        Vector2 folded = FoldOntoOctahedron(direction);
        int size = IrradianceProbes.DistanceSize;
        int x = Math.Clamp((int)(((folded.X * 0.5f) + 0.5f) * size), 0, size - 1);
        int y = Math.Clamp((int)(((folded.Y * 0.5f) + 0.5f) * size), 0, size - 1);
        return probes.Distances[(y * size) + x];
    }

    public void Dispose() => _scene.Dispose();

    private static Vector2 FoldOntoOctahedron(Vector3 direction)
    {
        float sum = MathF.Abs(direction.X) + MathF.Abs(direction.Y) + MathF.Abs(direction.Z);
        Vector2 folded = new(direction.X / sum, direction.Y / sum);
        if (direction.Z >= 0)
        {
            return folded;
        }

        float signX = folded.X >= 0 ? 1 : -1;
        float signY = folded.Y >= 0 ? 1 : -1;
        return new Vector2((1 - MathF.Abs(folded.Y)) * signX, (1 - MathF.Abs(folded.X)) * signY);
    }
}
