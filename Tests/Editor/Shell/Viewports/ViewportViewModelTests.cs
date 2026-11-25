using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Tests.Viewports;

public sealed class ViewportViewModelTests : IDisposable
{
    private const int Width = ViewportViewModelTestsBuilder.Width;
    private const int Height = ViewportViewModelTestsBuilder.Height;

    private readonly ViewportViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData(ViewportKey.Q, GizmoMode.Select)]
    [InlineData(ViewportKey.W, GizmoMode.Move)]
    [InlineData(ViewportKey.E, GizmoMode.Rotate)]
    [InlineData(ViewportKey.R, GizmoMode.Scale)]
    public void SwitchesGizmoModeWithQwer(ViewportKey key, GizmoMode mode)
    {
        _builder.ViewModel.GizmoMode = GizmoMode.Select;
        if (mode == GizmoMode.Select)
        {
            _builder.ViewModel.GizmoMode = GizmoMode.Move;
        }

        _builder.ViewModel.KeyPressed(key, ViewportButtons.None);

        _builder.ViewModel.GizmoMode.Should().Be(mode);
    }

    [Fact]
    public void LeavesQwerToTheCameraWhileTheRightButtonIsHeld()
    {
        _builder.ViewModel.GizmoMode = GizmoMode.Select;

        _builder.ViewModel.KeyPressed(ViewportKey.W, ViewportButtons.Right);

        _builder.ViewModel.GizmoMode.Should().Be(GizmoMode.Select);
    }

    [Fact]
    public void FramesTheSelectionWithF()
    {
        Entity first = _builder.AddPointLight(new Vector3(10, 0, 0));
        Entity second = _builder.AddPointLight(new Vector3(14, 0, 0));
        _builder.ViewModel.Selection.SelectAll([first, second]);

        _builder.ViewModel.KeyPressed(ViewportKey.F, ViewportButtons.None);

        EditorCamera camera = _builder.ViewModel.Camera;
        Vector3.Distance(camera.Pivot, new Vector3(12, 0, 0)).Should().BeLessThan(1e-3f);
        camera.PivotDistance.Should().BeGreaterThan(2);
    }

    [Fact]
    public void FramesEverythingWhenNothingIsSelected()
    {
        _builder.OpenLampScene();
        _builder.ViewModel.Camera.LookAt(new Vector3(20, 5, 0), 0.3f, -0.4f, 40);
        _builder.ViewModel.Selection.Select(null);

        _builder.ViewModel.FrameSelected();

        Vector3.Distance(_builder.ViewModel.Camera.Pivot, Vector3.Zero).Should().BeLessThan(1e-3f);
    }

    [Fact]
    public void DuplicatesTheSelectionAndSelectsTheCopy()
    {
        Entity lamp = _builder.OpenLampScene();
        _builder.ViewModel.Selection.Select(lamp);

        _builder.ViewModel.DuplicateSelected();

        Entity copy = _builder.ViewModel.Selection.Selected!.Value;
        copy.Should().NotBe(lamp);
        _builder.World.Get<EntityName>(copy).Value.Should().Be("Lamp 2");
        _builder.ViewModel.Scene.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void DuplicatesASelectedTreeOnceWhenWhatIsNestedInItIsSelectedToo()
    {
        _builder.OpenScene("""{ "entities": [ { "name": "Table", "components": { "transform": {} } }, { "name": "Cup", "parent": "Table", "components": { "transform": {} } } ] }""");
        Entity table = _builder.ViewModel.Scene.Find("Table")!.Value;
        Entity cup = _builder.ViewModel.Scene.Find("Cup")!.Value;
        _builder.ViewModel.Selection.SelectAll([table, cup], cup);

        _builder.ViewModel.DuplicateSelected();

        _builder.ViewModel.Scene.Entities.Should().HaveCount(4);
        Entity copy = _builder.ViewModel.Scene.Find("Table 2")!.Value;
        _builder.ViewModel.Selection.Entities.Should().Equal(copy);
        _builder.ViewModel.Scene.GetParent(_builder.ViewModel.Scene.Find("Cup 2")!.Value).Should().Be(copy);
    }

    [Fact]
    public void DropsAModelOnTheGroundUnderTheMouseAndSelectsIt()
    {
        _builder.OpenLampScene();
        _builder.ViewModel.Camera.LookAt(Vector3.Zero, yaw: 0.4f, pitch: -0.6f, distance: 10);
        Vector3 ground = new(2, 0, -3);
        Vector2 pixel = _builder.PixelOf(ground);

        _builder.ViewModel.DropAsset("Models/Fox.glb", pixel, Width, Height);

        Entity fox = _builder.ViewModel.Selection.Selected!.Value;
        _builder.World.Get<EntityName>(fox).Value.Should().Be("Fox");
        _builder.World.Get<MeshRenderer>(fox).Mesh.Should().Be("Models/Fox.glb");
        Vector3 position = _builder.World.Get<Transform>(fox).Position;
        Vector3.Distance(position, ground).Should().BeLessThan(0.01f);
        _builder.ViewModel.Scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void DropsAVehicleOnTheGroundUnderTheMouseAndSelectsIt()
    {
        _builder.WithVehicle("Vans/Van.fvvehicle");
        _builder.OpenLampScene();
        _builder.ViewModel.Camera.LookAt(Vector3.Zero, yaw: 0.4f, pitch: -0.6f, distance: 10);
        Vector3 ground = new(2, 0, -3);
        Vector2 pixel = _builder.PixelOf(ground);

        _builder.ViewModel.DropAsset("Vans/Van.fvvehicle", pixel, Width, Height);

        Entity van = _builder.ViewModel.Selection.Selected!.Value;
        _builder.World.Get<EntityName>(van).Value.Should().Be("Van");
        _builder.World.Get<Vehicle>(van).File.Should().Be("Vans/Van.fvvehicle");
        Vector3 position = _builder.World.Get<Transform>(van).Position;
        Vector3.Distance(position, ground).Should().BeLessThan(0.01f);
        CreationParts parts = _builder.ViewModel.Scene.Game.Services.GetService<CreationParts>()!;
        parts.FindAll(van).Should().HaveCount(5);
    }

    [Fact]
    public void FramesASelectedVehicleByItsParts()
    {
        _builder.WithVehicle("Vans/Van.fvvehicle");
        _builder.OpenScene("""{ "entities": [ { "name": "Van", "components": { "transform": {}, "vehicle": { "file": "Vans/Van.fvvehicle" } } } ] }""");
        Entity van = _builder.ViewModel.Scene.Find("Van")!.Value;
        _builder.ViewModel.Scene.Picker.TryGetBounds(_builder.World, van, out Vector3 min, out Vector3 max);
        _builder.ViewModel.Selection.Select(van);

        _builder.ViewModel.FrameSelected();

        Vector3 center = (min + max) / 2;
        Vector3.Distance(_builder.ViewModel.Camera.Pivot, center).Should().BeLessThan(1e-3f);
        center.Y.Should().BeGreaterThan(0.1f);
    }

    [Fact]
    public void NamesEachDroppedCopyOfAModelApart()
    {
        _builder.OpenLampScene();
        Vector2 pixel = _builder.PixelOf(Vector3.Zero);

        _builder.ViewModel.DropAsset("Fox.glb", pixel, Width, Height);
        _builder.ViewModel.DropAsset("Fox.glb", pixel, Width, Height);

        Entity second = _builder.ViewModel.Selection.Selected!.Value;
        _builder.World.Get<EntityName>(second).Value.Should().Be("Fox 2");
    }

    [Fact]
    public void DropsAModelAtThePivotDistanceWhereTheMouseMissesTheGround()
    {
        _builder.OpenLampScene();
        _builder.ViewModel.Camera.LookAt(new Vector3(0, 5, 0), yaw: 0, pitch: 0.3f, distance: 4);
        Vector2 pixel = _builder.PixelOf(new Vector3(0, 5, 0));

        _builder.ViewModel.DropAsset("Fox.glb", pixel, Width, Height);

        Entity fox = _builder.ViewModel.Selection.Selected!.Value;
        Vector3 position = _builder.World.Get<Transform>(fox).Position;
        Vector3.Distance(position, _builder.ViewModel.Camera.Pivot).Should().BeLessThan(0.01f);
    }

    [Fact]
    public void AsksForASceneBeforeDroppingAModel()
    {
        _builder.ViewModel.DropAsset("Fox.glb", Vector2.Zero, Width, Height);

        _builder.ViewModel.Scene.Entities.Should().BeEmpty();
        _builder.ViewModel.Status.Should().Contain("Open a scene");
    }

    [Fact]
    public void PreviewsTheSelectedCamerasView()
    {
        Transform transform = new() { Position = new Vector3(2, 3, 4) };
        Camera camera = new() { FieldOfView = 40 };
        Entity entity = _builder.World.Create(transform, camera);
        _builder.ViewModel.Selection.Select(entity);

        RenderCamera? preview = _builder.ViewModel.GetPreviewCamera();

        preview.Should().NotBeNull();
        preview!.Value.Camera.FieldOfView.Should().Be(40);
        preview.Value.Transform.Position.Should().Be(new Vector3(2, 3, 4));
    }

    [Fact]
    public void PreviewsNothingWhenTheSelectionIsNoCamera()
    {
        Entity light = _builder.AddPointLight(Vector3.Zero);
        _builder.ViewModel.Selection.Select(light);

        RenderCamera? preview = _builder.ViewModel.GetPreviewCamera();

        preview.Should().BeNull();
    }

    [Fact]
    public void PreviewsNothingInTheGameView()
    {
        Entity entity = _builder.World.Create(new Transform(), new Camera());
        _builder.ViewModel.Selection.Select(entity);
        _builder.ViewModel.IsGameView = true;

        RenderCamera? preview = _builder.ViewModel.GetPreviewCamera();

        preview.Should().BeNull("the game view shows no selection");
    }

    [Fact]
    public void SelectsWhatIsClicked()
    {
        Entity light = _builder.AddPointLight(new Vector3(1, 0.5f, 0));
        Vector2 pixel = _builder.PixelOf(new Vector3(1, 0.5f, 0));

        _builder.Click(pixel);

        _builder.ViewModel.Selection.Selected.Should().Be(light);
    }

    [Fact]
    public void ClearsTheSelectionWhenNothingIsClicked()
    {
        _builder.AddPointLight(new Vector3(1, 0.5f, 0));
        Vector2 pixel = _builder.PixelOf(new Vector3(1, 0.5f, 0));
        _builder.Click(pixel);

        _builder.Click(new Vector2(5, 5));

        _builder.ViewModel.Selection.Selected.Should().BeNull();
    }

    [Fact]
    public void MovesTheSelectionWithTheGizmo()
    {
        Entity light = _builder.AddPointLight(Vector3.Zero);
        Vector2 centre = _builder.PixelOf(Vector3.Zero);
        _builder.Click(centre);
        _builder.ViewModel.GizmoMode = GizmoMode.Move;
        float length = _builder.ViewModel.Gizmo.GetHandleLength(_builder.ViewModel.Camera, Vector3.Zero, Height);
        Vector2 handle = _builder.PixelOf(new Vector3(0, 0.7f * length, 0));
        Vector2 raised = _builder.PixelOf(new Vector3(0, (0.7f * length) + 2, 0));

        _builder.ViewModel.PointerPressed(handle, Width, Height);
        _builder.ViewModel.PointerMoved(raised, Width, Height);
        _builder.ViewModel.PointerReleased();

        Transform transform = _builder.World.Get<Transform>(light);
        Vector3.Distance(transform.Position, new Vector3(0, 2, 0)).Should().BeLessThan(0.02f);
        _builder.ViewModel.Selection.Selected.Should().Be(light);
        _builder.ViewModel.Scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void PicksAShotsKeyAndMovesItWithTheGizmo()
    {
        Entity shot = _builder.AddSelectedShot();
        _builder.ViewModel.GizmoMode = GizmoMode.Move;
        Vector3 key = new(2, 0, 0);
        _builder.Click(_builder.PixelOf(key));
        float length = _builder.ViewModel.Gizmo.GetHandleLength(_builder.ViewModel.Camera, key, Height);
        Vector2 handle = _builder.PixelOf(key + new Vector3(0, 0.7f * length, 0));
        Vector2 raised = _builder.PixelOf(key + new Vector3(0, (0.7f * length) + 1, 0));

        _builder.ViewModel.PointerPressed(handle, Width, Height);
        _builder.ViewModel.PointerMoved(raised, Width, Height);
        _builder.ViewModel.PointerReleased();

        _builder.ViewModel.Timeline.SelectedKey.Should().Be(1);
        Vector3 moved = _builder.World.Get<Shot>(shot).Keys[1].Position;
        Vector3.Distance(moved, new Vector3(2, 1, 0)).Should().BeLessThan(0.02f);
        _builder.World.Get<Transform>(shot).Position.Should().Be(Vector3.Zero, "the shot itself stays put");
        _builder.ViewModel.Selection.Selected.Should().Be(shot);
    }

    [Fact]
    public void PreviewsASelectedShotWhereItsTimelineIsScrubbed()
    {
        _builder.AddSelectedShot();

        _builder.ViewModel.Timeline.ScrubTime = 1;
        RenderCamera? preview = _builder.ViewModel.GetPreviewCamera();

        preview!.Value.Transform.Position.X.Should().BeApproximately(1, 1e-3f);
    }

    [Fact]
    public void AddsAShotKeyWhereTheViewStands()
    {
        Entity shot = _builder.AddSelectedShot();
        _builder.ViewModel.Timeline.ScrubTime = 3;

        _builder.ViewModel.AddShotKeyCommand.Execute(null);

        ShotKey added = _builder.World.Get<Shot>(shot).Keys[^1];
        added.Time.Should().Be(3);
        Vector3.Distance(added.Position, _builder.ViewModel.Camera.Position).Should().BeLessThan(1e-3f);
    }

    [Fact]
    public void TogglesTheGameViewWithG()
    {
        _builder.ViewModel.KeyPressed(ViewportKey.G, ViewportButtons.None);

        _builder.ViewModel.IsGameView.Should().BeTrue();
        _builder.ViewModel.IsGridVisible.Should().BeFalse();

        _builder.ViewModel.KeyPressed(ViewportKey.G, ViewportButtons.None);

        _builder.ViewModel.IsGameView.Should().BeFalse();
        _builder.ViewModel.IsGridVisible.Should().BeTrue();
    }

    [Fact]
    public void DrawsNoIconsSelectionOrGizmoInTheGameView()
    {
        Entity light = _builder.AddPointLight(new Vector3(1, 0.5f, 0));
        _builder.ViewModel.Selection.Select(light);
        _builder.ViewModel.GizmoMode = GizmoMode.Move;
        _builder.ViewModel.IsGameView = true;
        OverlayBuilder overlay = new();

        _builder.ViewModel.BuildOverlay(overlay, Height);

        overlay.Icons.IsEmpty.Should().BeTrue();
        overlay.Vertices.IsEmpty.Should().BeTrue();
    }
}
