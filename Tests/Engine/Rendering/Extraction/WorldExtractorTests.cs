using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Greyboxes;

namespace FlickerVoid.Engine.Rendering.Tests.Extraction;

public sealed class WorldExtractorTests : IDisposable
{
    private readonly WorldExtractorTestsBuilder _builder = new();

    [Fact]
    public void CopiesCamerasWithTheirTransforms()
    {
        Camera camera = new() { OrthographicHeight = 7 };
        Vector3 position = new(1, 2, 3);

        RenderScene scene = _builder.WithCamera(camera, position).Extract();

        scene.Cameras.Should().ContainSingle();
        scene.Cameras[0].Camera.OrthographicHeight.Should().Be(7);
        scene.Cameras[0].Transform.Position.Should().Be(position);
    }

    [Fact]
    public void DrawsAGreyboxAsABoxOfItsSize()
    {
        Greybox greybox = new() { Size = new Vector3(4, 3, 0.2f) };
        Transform transform = new() { Position = new Vector3(1, 0, 2) };

        RenderScene scene = _builder.WithGreybox(greybox, transform).Extract();

        scene.Meshes.Should().ContainSingle();
        scene.Meshes[0].Mesh.Should().Be(GreyboxMesh.GetName(new Vector3(4, 3, 0.2f)));
        scene.Meshes[0].World.Translation.Should().Be(new Vector3(1, 0, 2));
    }

    [Fact]
    public void DrawsTheSwapModelShownInAGreyboxsPlace()
    {
        Greybox greybox = new() { Swaps = ["Walls/Plain.glb", "Walls/Broken.glb"], Swap = 2 };

        RenderScene scene = _builder.WithGreybox(greybox, new Transform()).Extract();

        scene.Meshes.Should().ContainSingle();
        scene.Meshes[0].Mesh.Should().Be("Walls/Broken.glb");
    }

    [Fact]
    public void FlagsTheMeshesAndGreyboxesOfGreyedEntities()
    {
        Greybox greybox = new();

        RenderScene scene = _builder.WithMesh("Van.glb", new Transform()).WithGreybox(greybox, new Transform()).MarkAll(new Greyed()).Extract();

        scene.Meshes.Should().HaveCount(2).And.OnlyContain(mesh => mesh.Flags.HasFlag(MeshFlags.Greyed));
    }

    [Fact]
    public void DrawsNeitherTheOutlinesNorTheDoorwaysOfGreyedEntities()
    {
        Outline outline = new() { Color = new Vector3(1, 0, 0) };
        Doorway doorway = new();

        RenderScene scene = _builder.WithMesh("Door.glb", new Transform(), outline).WithDoorway(doorway, new Transform()).MarkAll(new Greyed()).Extract();

        scene.Meshes.Should().ContainSingle("a greyed level shows no markers");
        scene.Meshes[0].Outline.Should().BeNull();
    }

    [Fact]
    public void LeavesOutTheMeshesAndLightsOfUndrawnEntities()
    {
        Greybox greybox = new();
        PointLight point = new();
        SpotLight spot = new();

        RenderScene scene = _builder.WithMesh("Van.glb", new Transform()).WithGreybox(greybox, new Transform()).WithPointLight(point, Vector3.Zero).WithSpotLight(spot, new Transform()).MarkAll(new Undrawn()).Extract();

        scene.Meshes.Should().BeEmpty();
        scene.PointLights.Should().BeEmpty();
        scene.SpotLights.Should().BeEmpty();
    }

    [Fact]
    public void PutsTheActiveCameraFirstAtItsPosedView()
    {
        Camera still = new() { OrthographicHeight = 7 };
        Camera directed = new() { OrthographicHeight = 3 };
        Camera posedLens = new() { OrthographicHeight = 5 };
        Transform pose = new() { Position = new Vector3(4, 5, 6) };
        ActiveCamera active = new() { Lens = posedLens, Pose = pose };

        RenderScene scene = _builder.WithCamera(still, Vector3.Zero).WithActiveCamera(directed, Vector3.One, active).Extract();

        scene.Cameras.Should().HaveCount(2);
        scene.Cameras[0].Camera.OrthographicHeight.Should().Be(5);
        scene.Cameras[0].Transform.Position.Should().Be(new Vector3(4, 5, 6));
        scene.Cameras[1].Camera.OrthographicHeight.Should().Be(7);
    }

    [Fact]
    public void CopiesTheTransitionTheActiveCameraLaysOver()
    {
        Camera lens = new() { OrthographicHeight = 3 };
        Transform from = new() { Position = new Vector3(1, 2, 3) };
        ActiveCamera active = new() { Lens = lens, Fade = 0.25f, FadeColor = Vector3.UnitX, Dissolve = 0.5f, DissolveLens = lens, DissolvePose = from };

        RenderScene scene = _builder.WithActiveCamera(lens, Vector3.Zero, active).Extract();

        scene.Transition.Fade.Should().Be(0.25f);
        scene.Transition.FadeColor.Should().Be(Vector3.UnitX);
        scene.Transition.Dissolve.Should().Be(0.5f);
        scene.Transition.DissolveFrom.Transform.Position.Should().Be(new Vector3(1, 2, 3));
        scene.Transition.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void CopiesTheActiveCamerasDepthFadeAndFocus()
    {
        Camera lens = new() { OrthographicHeight = 3 };
        DepthFade fade = new(1, 2, 0.5f, 3, 4, 0.25f, Vector3.UnitY);
        ActiveCamera active = new() { Lens = lens, DepthFade = fade, Focus = new Vector3(1, 2, 3) };

        RenderScene scene = _builder.WithMainCamera(new MainCamera()).WithActiveCamera(lens, Vector3.Zero, active).Extract();

        scene.DepthFade.Should().Be(fade, "the director already chose the fade");
        scene.Focus.Should().Be(new Vector3(1, 2, 3));
    }

    [Fact]
    public void FadesTheFirstCameraAsTheMainCameraSaysWithoutADirector()
    {
        MainCamera main = new() { NearAmount = 0.5f };

        RenderScene scene = _builder.WithMainCamera(main).Extract();

        scene.DepthFade.Should().Be(main.ToDepthFade());
        scene.Focus.Should().BeNull();
    }

    [Fact]
    public void LaysNothingOverWithoutADirector()
    {
        RenderScene scene = _builder.WithCamera(new Camera(), Vector3.Zero).Extract();

        scene.Transition.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void FindsTheActiveCameraElseTheFirst()
    {
        Camera still = new() { OrthographicHeight = 7 };
        RenderCamera? first = _builder.WithCamera(still, Vector3.Zero).FindCamera();
        Transform pose = new() { Position = new Vector3(4, 5, 6) };
        ActiveCamera active = new() { Lens = still, Pose = pose };

        RenderCamera? directed = _builder.WithActiveCamera(still, Vector3.One, active).FindCamera();

        first!.Value.Transform.Position.Should().Be(Vector3.Zero);
        directed!.Value.Transform.Position.Should().Be(new Vector3(4, 5, 6));
    }

    [Fact]
    public void CopiesStaticMeshesWithTheirWorldMatrix()
    {
        Transform transform = new() { Position = new Vector3(1, 0, 0), Scale = new Vector3(2) };

        RenderScene scene = _builder.WithMesh("Cube.glb", transform).Extract();

        Matrix4x4 expected = Matrix4x4.CreateScale(2) * Matrix4x4.CreateTranslation(1, 0, 0);
        scene.Meshes.Should().ContainSingle();
        scene.Meshes[0].Mesh.Should().Be("Cube.glb");
        scene.Meshes[0].World.Should().Be(expected);
        scene.Meshes[0].Animator.Should().BeNull();
    }

    [Fact]
    public void CopiesTheOutlineOfOutlinedMeshes()
    {
        Outline outline = new() { Color = new Vector3(0, 1, 0) };

        RenderScene scene = _builder.WithMesh("Cube.glb", new Transform(), outline).WithMesh("Plain.glb", new Transform()).Extract();

        scene.Meshes.Should().HaveCount(2);
        RenderMesh outlined = scene.Meshes.Single(mesh => mesh.Mesh == "Cube.glb");
        RenderMesh plain = scene.Meshes.Single(mesh => mesh.Mesh == "Plain.glb");
        outlined.Outline!.Value.Color.Should().Be(new Vector3(0, 1, 0));
        plain.Outline.Should().BeNull();
    }

    [Fact]
    public void LeavesOutMeshesWithoutAFile()
    {
        RenderScene scene = _builder.WithMesh("", new Transform()).WithAnimatedMesh("", new MeshAnimator(), out _).Extract();

        scene.Meshes.Should().BeEmpty();
    }

    [Fact]
    public void CopiesAnimatedMeshesWithTheirAnimatorAndEntityId()
    {
        MeshAnimator animator = new() { Animation = "Walk", Time = 0.5f };

        RenderScene scene = _builder.WithAnimatedMesh("Character.glb", animator, out int id).Extract();

        scene.Meshes.Should().ContainSingle();
        scene.Meshes[0].Id.Should().Be(id);
        scene.Meshes[0].Animator!.Value.Animation.Should().Be("Walk");
    }

    [Fact]
    public void PutsStaticMeshesBeforeAnimatedOnes()
    {
        RenderScene scene = _builder.WithAnimatedMesh("Character.glb", new MeshAnimator(), out _).WithMesh("Cube.glb", new Transform()).Extract();

        scene.Meshes.Select(mesh => mesh.Mesh).Should().Equal("Cube.glb", "Character.glb");
    }

    [Fact]
    public void CopiesPointAndSpotLights()
    {
        PointLight point = new() { Range = 3 };
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1);
        SpotLight spot = new() { Cookie = "Window.png" };
        Transform spotTransform = new() { Position = Vector3.UnitY, Rotation = rotation };

        RenderScene scene = _builder.WithPointLight(point, Vector3.UnitX).WithSpotLight(spot, spotTransform).Extract();

        scene.PointLights.Should().ContainSingle();
        scene.PointLights[0].Light.Range.Should().Be(3);
        scene.PointLights[0].Position.Should().Be(Vector3.UnitX);
        scene.SpotLights.Should().ContainSingle();
        scene.SpotLights[0].Light.Cookie.Should().Be("Window.png");
        scene.SpotLights[0].Rotation.Should().Be(rotation);
    }

    [Fact]
    public void CopiesIrradianceVolumesWithTheirCenter()
    {
        IrradianceVolume volume = new() { Probes = "Room.fvprobes" };

        RenderScene scene = _builder.WithVolume(volume, new Vector3(0, 2, 0)).Extract();

        scene.ProbeVolumes.Should().ContainSingle();
        scene.ProbeVolumes[0].Volume.Probes.Should().Be("Room.fvprobes");
        scene.ProbeVolumes[0].Center.Should().Be(new Vector3(0, 2, 0));
    }

    [Fact]
    public void ReplacesTheLastFramesContent()
    {
        _builder.WithMesh("Cube.glb", new Transform()).Extract();

        RenderScene scene = _builder.Extract();

        scene.Meshes.Should().ContainSingle();
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
