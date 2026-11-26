using System.Numerics;

using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds scenes lit only through an irradiance volume's probes on a <see cref="SceneRendererTestsBuilder"/>, skipping the test where
/// the device has no ray queries. Two rooms side by side seen from above (32 by 32 pixels, a quarter unit each, the top row at z -4):
/// room A north of a wall along z 0 and room B south of it, the wall's doorway (x from -1 to 1) open or closed by a door. A red wall
/// stands in room A at x 1 facing west, beside the doorway, lit by a sun shining east, which no floor and nothing in room B faces.
/// Or a white wall seen from the front with a panel glowing green on the floor before it. No ambient light, so all else is dark.
/// </summary>
internal sealed class ProbePassTestsBuilder : IDisposable
{
    public const int Size = 32;

    private const string Above = """{ "name": "Camera", "components": { "transform": { "position": [0, 6, 0], "rotation": [-0.70710677, 0, 0, 0.70710677] }, "camera": { "orthographicHeight": 8 } } }""";
    private const string Floor = """{ "name": "Floor", "components": { "transform": { "rotation": [-0.70710677, 0, 0, 0.70710677], "scale": [8, 8, 1] }, "meshRenderer": { "mesh": "Floor.glb" } } }""";
    private const string WestWall = """{ "name": "West Wall", "components": { "transform": { "position": [-2.5, 0, 0] }, "meshRenderer": { "mesh": "$greybox:3,2,0.2" } } }""";
    private const string EastWall = """{ "name": "East Wall", "components": { "transform": { "position": [2.5, 0, 0] }, "meshRenderer": { "mesh": "$greybox:3,2,0.2" } } }""";
    private const string Door = """{ "name": "Door", "components": { "transform": {}, "meshRenderer": { "mesh": "$greybox:2,2,0.2" } } }""";
    private const string RedWall = """{ "name": "Red Wall", "components": { "transform": { "position": [1, 0, -1.55] }, "meshRenderer": { "mesh": "$greybox:0.2,2,2.9", "materials": ["Red.fvmat"] } } }""";
    private const string Rooms = """{ "name": "Volume", "components": { "transform": { "position": [0, 1, 0] }, "irradianceVolume": { "size": [8, 2, 8], "probesX": 8, "probesY": 2, "probesZ": 8, "probes": "Rooms.fvprobes" } } }""";

    private const string Front = """{ "name": "Camera", "components": { "transform": { "position": [0, 1.5, 5] }, "camera": { "orthographicHeight": 4 } } }""";
    private const string WhiteWall = """{ "name": "Wall", "components": { "transform": { "position": [0, 0, -0.1] }, "meshRenderer": { "mesh": "$greybox:4,3,0.2" } } }""";
    private const string Panel = """{ "name": "Panel", "components": { "transform": { "position": [0, 0, 0.6] }, "meshRenderer": { "mesh": "$greybox:2,0.05,1", "materials": ["Glow.fvmat"] } } }""";
    private const string Hall = """{ "name": "Volume", "components": { "transform": { "position": [0, 1.5, 1] }, "irradianceVolume": { "size": [4, 3, 2], "probesX": 4, "probesY": 3, "probesZ": 2 } } }""";

    private readonly SceneRendererTestsBuilder _scene = new();

    public ProbePassTestsBuilder()
    {
        _scene.WithRayQueries().WithTargetSize(Size);
        _scene.WithModel("Floor.glb", static model => model.WithQuad(1).WithBaseColor(Vector4.One));
        _scene.WithFile("Red.fvmat", """{ "parameters": { "tint": [1, 0, 0] } }""");
        _scene.WithFile("Glow.fvmat", """{ "parameters": { "tint": [0, 0, 0], "emissive": [0, 1, 0], "emissiveIntensity": 4 } }""");
    }

    /// <summary>The pixel of room A's floor just west of the red wall.</summary>
    public static int FloorBesideTheRedWall => (10 * Size) + 18;

    /// <summary>The pixel of room B's floor in the doorway's mouth, just south of the wall between the rooms.</summary>
    public static int FloorBehindTheDoor => (16 * Size) + 16;

    /// <summary>The pixel of the white wall just above the glowing panel.</summary>
    public static int WallAboveThePanel => (26 * Size) + 16;

    public ProbePassTestsBuilder WithRooms(bool doorOpen)
    {
        List<string> entities = [Above, Floor, WestWall, EastWall, RedWall, Rooms];
        if (!doorOpen)
        {
            entities.Add(Door);
        }

        _scene.WithScene([.. entities]);
        _scene.WithLight(sunDirection: Vector3.UnitX, ambient: 0);
        return this;
    }

    public ProbePassTestsBuilder WithGlowingPanel()
    {
        _scene.WithScene(Front, WhiteWall, Panel, Hall);
        _scene.WithLight(sunDirection: Vector3.UnitX, ambient: 0);
        return this;
    }

    /// <summary>Draws with the ray traced tier, whose probes follow the light live.</summary>
    public ProbePassTestsBuilder Traced()
    {
        _scene.WithRenderer(static renderer => renderer.RayTracing = true);
        return this;
    }

    /// <summary>Draws with the base tier, which lights with the probes baked into the volume's file.</summary>
    public ProbePassTestsBuilder Untraced()
    {
        _scene.WithRenderer(static renderer => renderer.RayTracing = false);
        return this;
    }

    public ProbePassTestsBuilder WithoutProbes()
    {
        _scene.WithRenderer(static renderer =>
        {
            renderer.RayTracing = true;
            renderer.Probes = false;
        });
        return this;
    }

    /// <summary>Bakes the rooms' volume through rays and writes its probes file, which the base tier lights with.</summary>
    public ProbePassTestsBuilder Baked()
    {
        IrradianceVolume volume = new() { Size = new Vector3(8, 2, 8), ProbesX = 8, ProbesY = 2, ProbesZ = 8 };
        using IrradianceBaker baker = _scene.BuildBaker();
        IrradianceProbes probes = baker.Bake(_scene.World, volume, Vector3.UnitY);
        _scene.WithProbes("Rooms.fvprobes", probes);
        return this;
    }

    /// <summary>Renders that many frames in a row and gives the last one's pixels.</summary>
    public uint[] Render(int frames) => _scene.RenderFrames(frames);

    public void Dispose() => _scene.Dispose();
}
