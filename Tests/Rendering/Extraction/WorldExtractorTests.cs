using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;

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
