using System.Numerics;

using Arch.Core;


using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Inspector;

public sealed class InspectorViewModelTests : IDisposable
{
    private readonly InspectorViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheTemplatePickerShowsAndSetsTheTemplateTheSelectedEntityStartsFrom()
    {
        _builder.WithContentFile("Templates/Glow.fventity");
        Entity box = _builder.OpenScene("""{ "entities": [ { "name": "Box", "components": { "transform": {} } } ] }""");
        AssetPickerViewModel? before = _builder.ViewModel.Template;

        before!.Choose("Templates/Glow.fventity");

        before.Current.Should().Be("Templates/Glow.fventity");
        _builder.Scene.GetTemplate(box).Should().Be("Templates/Glow.fventity");
        _builder.ViewModel.Template!.Current.Should().Be("Templates/Glow.fventity");
    }

    [Fact]
    public void AnEntityOutsideTheSceneFileHasNoTemplatePicker()
    {
        _builder.Select(new Transform());

        _builder.ViewModel.Template.Should().BeNull();
    }

    [Fact]
    public void ShowsTheRegisteredComponentsOfTheSelectedEntity()
    {
        _builder.Select("Lamp", new Transform(), new PointLight());

        string[] titles = [.. _builder.ViewModel.Components.Select(component => component.Title)];
        string[] labels = [.. _builder.Component("Point Light").Fields.Select(field => field.Label)];
        _builder.ViewModel.Title.Should().Be("Lamp");
        titles.Should().Equal("Transform", "Point Light");
        labels.Should().Equal("Color", "Intensity", "Range", "Casts Shadows", "Source Radius");
    }

    [Fact]
    public void FilteringShowsOnlyTheComponentsWhoseNameOrFieldsMatch()
    {
        _builder.Select("Lamp", new Transform(), new PointLight());

        _builder.ViewModel.FilterText = "intens";
        string[] byField = [.. _builder.ViewModel.VisibleComponents.Select(component => component.Title)];
        _builder.ViewModel.FilterText = "transf";
        string[] byName = [.. _builder.ViewModel.VisibleComponents.Select(component => component.Title)];
        _builder.ViewModel.FilterText = "";

        byField.Should().Equal("Point Light");
        byName.Should().Equal("Transform");
        _builder.ViewModel.VisibleComponents.Should().HaveCount(2);
    }

    [Fact]
    public void TheCategoryRailShowsOneModulesComponentsAndResetsForAnotherEntity()
    {
        _builder.Select("Lamp", new Transform(), new PointLight());

        string[] categories = [.. _builder.ViewModel.Categories];
        _builder.ViewModel.SelectedCategory = "Rendering";
        string[] rendering = [.. _builder.ViewModel.VisibleComponents.Select(component => component.Title)];
        _builder.Select(new Transform());

        categories.Should().Equal("Rendering", "Runtime");
        rendering.Should().Equal("Point Light");
        _builder.ViewModel.SelectedCategory.Should().BeNull();
        _builder.ViewModel.HasCategories.Should().BeFalse();
    }

    [Fact]
    public void AddsAndRemovesComponents()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight());
        FlickerVoid.Engine.Runtime.Scenes.SceneComponent spot = _builder.ViewModel.AddableComponents.Single(component => component.Name == "spotLight");

        _builder.ViewModel.AddComponentCommand.Execute(spot);
        _builder.ViewModel.RemoveComponentCommand.Execute(_builder.Component("Point Light"));

        _builder.World.Has<SpotLight>(lamp).Should().BeTrue();
        _builder.World.Has<PointLight>(lamp).Should().BeFalse();
        _builder.ViewModel.AddableComponents.Should().NotContain(spot);
        _builder.Component("Transform").CanRemove.Should().BeFalse();
        _builder.Scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void OffersAddableComponentsGroupedByModuleWithIcons()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight());

        FlickerVoid.Editor.Core.Menus.MenuChoice spot = _builder.ViewModel.AddChoices.Single(choice => choice.Name == "Spot Light");
        spot.Run();

        spot.Group.Should().Be("Rendering");
        spot.Icon.Should().Be("SpotLight");
        _builder.World.Has<SpotLight>(lamp).Should().BeTrue();
        _builder.ViewModel.AddChoices.Should().NotContain(choice => choice.Name == "Spot Light" || choice.Name == "Transform");
    }

    [Fact]
    public void ShowsNothingWhenNothingIsSelected()
    {
        _builder.Select("Lamp", new Transform(), new PointLight());

        _builder.SelectNothing();

        _builder.ViewModel.Components.Should().BeEmpty();
        _builder.ViewModel.Title.Should().Be("Nothing selected");
    }

    [Fact]
    public void ChoosesAnEditorFromEachPropertysType()
    {
        _builder.Select("Eye", new Camera(), new MeshRenderer { Mesh = "Room.glb" });

        _builder.Field<EnumFieldViewModel>("Camera", "Projection").Options.Should().Contain(CameraProjection.Orthographic);
        _builder.Field<NumberFieldViewModel>("Camera", "Field Of View").Value.Should().Be(60);
        _builder.Field<AssetFieldViewModel>("Mesh Renderer", "Mesh").Picker.Current.Should().Be("Room.glb");
    }

    [Fact]
    public void WritesEditsIntoTheWorld()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight());

        _builder.Field<NumberFieldViewModel>("Point Light", "Intensity").Value = 3;
        _builder.Field<BoolFieldViewModel>("Point Light", "Casts Shadows").Value = true;
        _builder.Field<VectorFieldViewModel>("Transform", "Position").Y = 2;

        PointLight light = _builder.World.Get<PointLight>(lamp);
        Transform transform = _builder.World.Get<Transform>(lamp);
        light.Intensity.Should().Be(3);
        light.CastsShadows.Should().BeTrue();
        transform.Position.Should().Be(new Vector3(0, 2, 0));
    }

    [Fact]
    public void EditsGoThroughTheScene()
    {
        _builder.Select("Lamp", new Transform(), new PointLight());
        int edits = 0;
        _builder.Scene.Edited += () => edits++;

        _builder.Field<NumberFieldViewModel>("Point Light", "Intensity").Value = 3;

        edits.Should().Be(1);
        _builder.Scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void RefreshShowsChangesMadeElsewhere()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight());
        Transform moved = new() { Position = new Vector3(1, 2, 3) };
        _builder.World.Set(lamp, moved);

        _builder.ViewModel.Refresh();

        VectorFieldViewModel position = _builder.Field<VectorFieldViewModel>("Transform", "Position");
        position.X.Should().Be(1);
        position.Z.Should().Be(3);
    }

    [Fact]
    public void ClearsWhenTheSelectedEntityIsDestroyed()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight());
        _builder.World.Destroy(lamp);

        _builder.ViewModel.Refresh();

        _builder.ViewModel.Components.Should().BeEmpty();
    }

    [Fact]
    public void EditsRotationsAsAnglesInDegrees()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight());
        RotationFieldViewModel rotation = _builder.Field<RotationFieldViewModel>("Transform", "Rotation");

        rotation.X = 30;
        rotation.Y = 45;
        rotation.Z = 10;

        Quaternion stored = _builder.World.Get<Transform>(lamp).Rotation;
        Quaternion expected = Quaternion.CreateFromYawPitchRoll(float.DegreesToRadians(45), float.DegreesToRadians(30), float.DegreesToRadians(10));
        float alignment = MathF.Abs(Quaternion.Dot(stored, expected));
        alignment.Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void ShowsARotationMadeElsewhereAsTheAnglesThatMakeIt()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight());
        Quaternion turned = Quaternion.CreateFromYawPitchRoll(float.DegreesToRadians(-120), float.DegreesToRadians(20), float.DegreesToRadians(-35));
        Transform transform = new() { Rotation = turned };
        _builder.World.Set(lamp, transform);

        _builder.ViewModel.Refresh();

        RotationFieldViewModel rotation = _builder.Field<RotationFieldViewModel>("Transform", "Rotation");
        rotation.X.Should().BeApproximately(20, 0.01);
        rotation.Y.Should().BeApproximately(-120, 0.01);
        rotation.Z.Should().BeApproximately(-35, 0.01);
    }

    [Fact]
    public void EditsLightColoursWithAPickerInSrgb()
    {
        Entity lamp = _builder.Select("Lamp", new Transform(), new PointLight { Color = new Vector3(1, 0.2158605f, 0) });
        ColorFieldViewModel color = _builder.Field<ColorFieldViewModel>("Point Light", "Color");

        color.Color.Should().Be(new Vector3(1, 128 / 255f, 0));

        color.Color = new Vector3(0, 1, 0);

        PointLight light = _builder.World.Get<PointLight>(lamp);
        light.Color.Should().Be(new Vector3(0, 1, 0));
    }

    [Fact]
    public void LimitsNumbersTheComponentGivesARangeFor()
    {
        _builder.Select("Spot", new Transform(), new SpotLight());

        RangedNumberFieldViewModel outer = _builder.Field<RangedNumberFieldViewModel>("Spot Light", "Outer Cone Angle");

        outer.Minimum.Should().Be(0);
        outer.Maximum.Should().Be(90);
    }

    [Fact]
    public void ShowsFloatsAsTheyAreWritten()
    {
        _builder.Select("Lamp", new Transform { Position = new Vector3(-0.66f, 0.9f, 0) }, new PointLight { Intensity = 1.6f });

        _builder.Field<NumberFieldViewModel>("Point Light", "Intensity").Value.Should().Be(1.6);
        _builder.Field<VectorFieldViewModel>("Transform", "Position").X.Should().Be(-0.66);
    }

    [Fact]
    public void EditsAListOfFilesItemByItem()
    {
        Entity entity = _builder.Select("Crate", new Transform(), new MeshRenderer { Mesh = "Crate.glb", Materials = ["Wood.fvmat"] });
        ListFieldViewModel materials = _builder.Field<ListFieldViewModel>("Mesh Renderer", "Materials");

        materials.Items.Should().ContainSingle().Which.Text.Should().Be("Wood.fvmat");
        materials.AddCommand.Execute(null);
        materials.Items[1].Text = "Iron.fvmat";

        _builder.World.Get<MeshRenderer>(entity).Materials.Should().Equal("Wood.fvmat", "Iron.fvmat");

        materials.RemoveCommand.Execute(materials.Items[0]);

        _builder.World.Get<MeshRenderer>(entity).Materials.Should().Equal("Iron.fvmat");
    }

    [Fact]
    public void AnEmptyOptionalListIsNull()
    {
        Entity entity = _builder.Select("Crate", new Transform(), new MeshRenderer { Mesh = "Crate.glb", Materials = ["Wood.fvmat"] });
        ListFieldViewModel materials = _builder.Field<ListFieldViewModel>("Mesh Renderer", "Materials");

        materials.RemoveCommand.Execute(materials.Items[0]);

        _builder.World.Get<MeshRenderer>(entity).Materials.Should().BeNull();
    }

    [Fact]
    public void PicksTheContentFilesAFileFieldTakes()
    {
        _builder.WithContentFile("Props/Crate.glb").WithContentFile("Wood.fvmat").WithContentFile("Paper.png");
        _builder.Select("Crate", new Transform(), new MeshRenderer { Mesh = "Props/Crate.glb", Materials = ["Wood.fvmat"] });

        AssetFieldViewModel mesh = _builder.Field<AssetFieldViewModel>("Mesh Renderer", "Mesh");
        ListFieldViewModel materials = _builder.Field<ListFieldViewModel>("Mesh Renderer", "Materials");
        mesh.Picker.IsOpen = true;
        AssetPickerViewModel material = materials.Items[0].Picker!;
        material.IsOpen = true;

        mesh.Picker.Matches.Should().Equal("Props/Crate.glb");
        material.Matches.Should().Equal("Wood.fvmat");
        material.Current.Should().Be("Wood.fvmat");
    }

    [Fact]
    public void WritesTheAssetChosenInAPicker()
    {
        _builder.WithContentFile("Props/Crate.glb").WithContentFile("Room.glb").WithContentFile("Wood.fvmat").WithContentFile("Stone.fvmat");
        Entity crate = _builder.Select("Crate", new Transform(), new MeshRenderer { Mesh = "Props/Crate.glb", Materials = ["Wood.fvmat"] });

        _builder.Field<AssetFieldViewModel>("Mesh Renderer", "Mesh").Picker.ChooseCommand.Execute("Room.glb");
        _builder.Field<ListFieldViewModel>("Mesh Renderer", "Materials").Items[0].Picker!.ChooseCommand.Execute("Stone.fvmat");

        MeshRenderer renderer = _builder.World.Get<MeshRenderer>(crate);
        renderer.Mesh.Should().Be("Room.glb");
        renderer.Materials.Should().Equal("Stone.fvmat");
    }

    [Fact]
    public void EditsANestedValueThroughItsOwnFields()
    {
        CameraCue cue = new() { Transition = new ShotTransition { Kind = TransitionKind.Blend, Duration = 1 } };
        Entity spot = _builder.Select("Spot", new Transform(), cue);

        ObjectFieldViewModel transition = _builder.Field<ObjectFieldViewModel>("Camera Cue", "Transition");
        string[] labels = [.. transition.Fields.Select(field => field.Label)];
        NumberFieldViewModel duration = Child<NumberFieldViewModel>(transition, "Duration");
        duration.Value = 2;

        labels.Should().Equal("Kind", "Duration", "In Duration", "Color", "Ease");
        transition.HasKinds.Should().BeFalse();
        _builder.World.Get<CameraCue>(spot).Transition.Duration.Should().Be(2);
        _builder.World.Get<CameraCue>(spot).Transition.Kind.Should().Be(TransitionKind.Blend);
    }

    [Fact]
    public void EditsAddsAndRemovesTheItemsOfAListOfObjects()
    {
        List<ShotKey> keys = [new ShotKey { Time = 0 }, new ShotKey { Time = 2 }];
        Entity hall = _builder.Select("Hall", new Transform(), new Shot { Keys = keys });

        CollectionFieldViewModel field = _builder.Field<CollectionFieldViewModel>("Shot", "Keys");
        ObjectFieldViewModel second = field.Items[1].Should().BeOfType<ObjectFieldViewModel>().Subject;
        Child<NumberFieldViewModel>(second, "Time").Value = 3;
        field.AddCommand.Execute(null);
        _builder.ViewModel.Refresh();
        field.RemoveCommand.Execute(field.Items[0]);
        _builder.ViewModel.Refresh();

        float[] times = [.. _builder.World.Get<Shot>(hall).Keys.Select(key => key.Time)];
        times.Should().Equal(3, 0);
        field.Items.Should().HaveCount(2);
        keys.Select(key => key.Time).Should().Equal([0, 2], "the edits write copies, so the undo step taken before each keeps the old keys");
    }

    [Fact]
    public void SwitchesAPolymorphicValueByKindKeepingWhatTheKindsShare()
    {
        BoxCollider box = new() { Offset = new Vector3(0.5f, 1, 0) };
        Entity door = _builder.Select("Door", new Transform(), new StaticBody { Collider = box });

        ObjectFieldViewModel collider = _builder.Field<ObjectFieldViewModel>("Static Body", "Collider");
        string[] boxLabels = [.. collider.Fields.Select(field => field.Label)];
        string? boxKind = collider.Kind;
        collider.Kind = "Sphere";
        _builder.ViewModel.Refresh();

        collider.Kinds.Should().Equal("Box", "Sphere", "Capsule", "Hull", "Triangles");
        boxKind.Should().Be("Box");
        boxLabels.Should().Equal("Size", "Offset");
        SphereCollider sphere = _builder.World.Get<StaticBody>(door).Collider.Should().BeOfType<SphereCollider>().Subject;
        sphere.Offset.Should().Be(new Vector3(0.5f, 1, 0));
        collider.Fields.Select(field => field.Label).Should().Equal("Radius", "Offset");
    }

    [Fact]
    public void EditsAListOfTagsAsNames()
    {
        Entity spot = _builder.Select("Spot", new Transform(), new TriggerArea { RequiredTags = [new Tag("Player")] });

        CollectionFieldViewModel tags = _builder.Field<CollectionFieldViewModel>("Trigger Area", "Required Tags");
        tags.AddCommand.Execute(null);
        _builder.ViewModel.Refresh();
        tags.Items[1].Should().BeOfType<TextFieldViewModel>().Subject.Value = "Character.Fox";

        tags.Items[0].Should().BeOfType<TextFieldViewModel>().Subject.Value.Should().Be("Player");
        _builder.World.Get<TriggerArea>(spot).RequiredTags.Should().Equal(new Tag("Player"), new Tag("Character.Fox"));
    }

    private static TField Child<TField>(ObjectFieldViewModel owner, string label)
        where TField : FieldViewModel
    {
        FieldViewModel field = owner.Fields.Single(candidate => candidate.Label == label);
        return field.Should().BeOfType<TField>().Subject;
    }
}
