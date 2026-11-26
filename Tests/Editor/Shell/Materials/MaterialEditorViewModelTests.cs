using System.Numerics;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Assets.Tests.Materials;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Inspector;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Engine.Rendering.Materials;

using NSubstitute;

namespace FlickerVoid.Editor.Tests.Materials;

public sealed class MaterialEditorViewModelTests : IDisposable
{
    private readonly MaterialEditorViewModelTestsBuilder _builder = new();

    private const string Master = """
        { "shadingModel": "Painted", "parameters": [{ "name": "grime", "type": "color", "default": [0, 0, 0] }] }
        """;

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ListsTheMaterialsOfTheContentFolder()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");

        MaterialEditorViewModel editor = _builder.Build();

        editor.Materials.Should().Equal("Brick.fvmat");
    }

    [Fact]
    public void ShowsAFieldForEveryParameterTheSceneShaderDeclares()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": { "wrap": 0.5 } }""");
        MaterialEditorViewModel editor = _builder.Build();

        editor.SelectedMaterial = "Brick.fvmat";

        string[] labels = [.. editor.Parameters.Select(field => field.Label)];
        labels.Should().StartWith(["Tint", "Ramp Softness", "Wrap", "Shadow Tint"]);
        NumberFieldViewModel wrap = editor.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Wrap");
        wrap.Value.Should().Be(0.5);
        wrap.Maximum.Should().Be(1);
        editor.Fields.Single(field => field.Label == "Tint").Should().BeOfType<ColorFieldViewModel>();
    }

    [Fact]
    public void PicksTheTextureAmongTheImagesAndPaintings()
    {
        _builder.WithMaterial("Wall.fvmat", """{ "parameters": { "texture": "Paintings/Wall.reb" } }""");
        _builder.WithMaterial("Paintings/Wall.reb", "painting");
        _builder.WithMaterial("Paper.png", "image");
        MaterialEditorViewModel editor = _builder.Build();

        editor.SelectedMaterial = "Wall.fvmat";

        AssetFieldViewModel texture = editor.Fields.OfType<AssetFieldViewModel>().Single(field => field.Label == "Texture");
        texture.Picker.Current.Should().Be("Paintings/Wall.reb");
        texture.Picker.IsOpen = true;
        texture.Picker.Matches.Should().Equal("Paintings/Wall.reb", "Paper.png");
        texture.Picker.AcceptsNone.Should().BeTrue();
    }

    [Fact]
    public void HoldsAnEditUnsavedAndTellsTheSceneTheMaterialChanged()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";
        List<string> changed = [];
        _builder.Scene.Content.Changed += changed.Add;

        NumberFieldViewModel specular = editor.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        specular.Value = 2;

        string onDisk = _builder.ReadMaterial("Brick.fvmat");
        onDisk.Should().NotContain("specular");
        changed.Should().ContainSingle().Which.Should().EndWith("Brick.fvmat");
        editor.IsUnsaved.Should().BeTrue();
        editor.Heading.Should().Be("Brick.fvmat *");
    }

    [Fact]
    public void SavesTheEditedMaterial()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";
        NumberFieldViewModel specular = editor.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        specular.Value = 2;

        editor.SaveCommand.Execute(null);

        string saved = _builder.ReadMaterial("Brick.fvmat");
        saved.Should().Contain("\"specular\": 2");
        editor.IsUnsaved.Should().BeFalse();
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void DiscardsTheEditsAndShowsTheSavedValues()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": { "specular": 0.5 } }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";
        NumberFieldViewModel edited = editor.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        edited.Value = 2;

        editor.DiscardCommand.Execute(null);

        editor.IsUnsaved.Should().BeFalse();
        NumberFieldViewModel shown = editor.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        shown.Value.Should().Be(0.5);
        string onDisk = _builder.ReadMaterial("Brick.fvmat");
        onDisk.Should().Contain("0.5");
    }

    [Fact]
    public void ShowsTheMaterialAgainWhenItsFileChangesOnDisk()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": { "specular": 0.5 } }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";

        File.WriteAllText(Path.Combine(_builder.Folder, "Brick.fvmat"), """{ "parameters": { "specular": 0.25 } }""");
        _builder.Content.NotifyChanged("Brick.fvmat");

        NumberFieldViewModel shown = editor.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        shown.Value.Should().Be(0.25);
    }

    [Fact]
    public void PreviewsTheEditedMaterialOnTheChosenShape()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";

        editor.Fields.OfType<ColorFieldViewModel>().Single(field => field.Label == "Emissive").Color = Vector3.One;
        editor.IsCube = true;

        _builder.Preview.Received().Render(
            Arg.Is<MaterialFile>(material => material.Parameters.ContainsKey("emissive")),
            Arg.Is<PreviewView>(view => view.Shape == PreviewShape.Cube),
            MaterialEditorViewModel.DefaultPreviewSize);
        editor.PreviewPixels.Should().NotBeNull();
    }

    [Fact]
    public void CreatesAMaterialAndSelectsIt()
    {
        MaterialEditorViewModel editor = _builder.Build();

        editor.CreateMaterialCommand.Execute(null);

        editor.Materials.Should().Equal("New Material.fvmat");
        editor.SelectedMaterial.Should().Be("New Material.fvmat");
    }

    [Fact]
    public void TurnsThePreviewWhenDragged()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";
        float before = editor.PreviewYaw;

        editor.DragPreview(new Vector2(50, 0));

        editor.PreviewYaw.Should().NotBe(before);
    }

    [Fact]
    public void ListsTheMaterialsUnderTheirMasters()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parent": "Masters/Surface.fvmaster", "parameters": {} }""");

        MaterialEditorViewModel editor = _builder.Build();

        editor.Tree.Should().ContainSingle().Which.Path.Should().Be("Masters/Surface.fvmaster");
        editor.Tree[0].Children.Should().ContainSingle().Which.Path.Should().Be("Brick.fvmat");
    }

    [Fact]
    public void OpensAMaterialPickedInTheTree()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parent": "Masters/Surface.fvmaster", "parameters": {} }""");
        _builder.WithMaterial("Masters/Surface.fvmaster", Master);
        MaterialEditorViewModel editor = _builder.Build();

        editor.SelectedNode = editor.Tree[0].Children[0];

        editor.SelectedMaterial.Should().Be("Brick.fvmat");
        editor.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void MakesAnInstanceOfTheMasterPickedAndOpensIt()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parent": "Masters/Surface.fvmaster", "parameters": {} }""");
        _builder.WithMaterial("Masters/Surface.fvmaster", Master);
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedNode = editor.Tree[0];

        editor.CreateInstanceCommand.Execute(null);

        editor.SelectedMaterial.Should().Be("Surface Instance.fvmat");
        editor.SelectedNode!.Path.Should().Be("Surface Instance.fvmat");
        _builder.ReadMaterial("Surface Instance.fvmat").Should().Contain("Masters/Surface.fvmaster");
    }

    [Fact]
    public void ShowsTheMastersParametersAfterTheSceneShaders()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parent": "Masters/Surface.fvmaster", "parameters": {} }""");
        _builder.WithMaterial("Masters/Surface.fvmaster", Master);
        MaterialEditorViewModel editor = _builder.Build();

        editor.SelectedMaterial = "Brick.fvmat";

        editor.Parameters[^1].Label.Should().Be("Grime");
        editor.Parameters[^1].Source.Should().Be(MaterialParameterValue.DefaultSource);
    }

    [Fact]
    public void ShowsTheParentsValueUntilTheMaterialOverridesIt()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": { "specular": 0.5 } }""");
        _builder.WithMaterial("Red Brick.fvmat", """{ "parent": "Brick.fvmat", "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Red Brick.fvmat";
        MaterialParameterViewModel specular = editor.Parameters.Single(parameter => parameter.Label == "Specular");

        specular.IsOverridden.Should().BeFalse();
        specular.Source.Should().Be("Brick.fvmat");
        ((NumberFieldViewModel)specular.Field).Value.Should().Be(0.5);

        ((NumberFieldViewModel)specular.Field).Value = 2;

        specular.IsOverridden.Should().BeTrue("editing a kept value overrides it");
        specular.IsOverridden = false;
        ((NumberFieldViewModel)specular.Field).Value.Should().Be(0.5);
        editor.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void OpensTheMaterialAgainWhenItsParentChanges()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        _builder.WithMaterial("Masters/Surface.fvmaster", Master);
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";

        AssetFieldViewModel parent = (AssetFieldViewModel)editor.Parent!;
        parent.Picker.Current.Should().BeNull();
        parent.Picker.IsOpen = true;
        parent.Picker.Matches.Should().Contain("Masters/Surface.fvmaster");
        parent.Picker.Choose("Masters/Surface.fvmaster");

        editor.Parameters[^1].Label.Should().Be("Grime");
        editor.Tree.Should().ContainSingle().Which.Path.Should().Be("Masters/Surface.fvmaster");
    }

    [Fact]
    public void PreviewsOnAPlaneInTheMoodPicked()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";

        editor.IsPlane = true;
        editor.Mood = PreviewMood.Night;

        editor.IsSphere.Should().BeFalse();
        _builder.Preview.Received().Render(
            Arg.Any<MaterialFile>(),
            Arg.Is<PreviewView>(view => view.Shape == PreviewShape.Plane && view.Mood == PreviewMood.Night),
            MaterialEditorViewModel.DefaultPreviewSize);
    }

    [Fact]
    public void TurnsTheLightWhenDragged()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";
        float shape = editor.PreviewYaw;

        editor.DragLight(new Vector2(50, 0));

        editor.LightYaw.Should().NotBe(0);
        editor.PreviewYaw.Should().Be(shape);
        _builder.Preview.Received().Render(Arg.Any<MaterialFile>(), Arg.Is<PreviewView>(view => view.LightYaw != 0), MaterialEditorViewModel.DefaultPreviewSize);
    }

    [Fact]
    public void RendersThePreviewAtTheSizeItIsGivenWithinItsLimits()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";

        editor.ResizePreview(500);
        editor.ResizePreview(5000);
        editor.ResizePreview(20);

        _builder.Preview.Received().Render(Arg.Any<MaterialFile>(), Arg.Any<PreviewView>(), 480);
        _builder.Preview.Received().Render(Arg.Any<MaterialFile>(), Arg.Any<PreviewView>(), MaterialEditorViewModel.MaximumPreviewSize);
        _builder.Preview.Received().Render(Arg.Any<MaterialFile>(), Arg.Any<PreviewView>(), MaterialEditorViewModel.MinimumPreviewSize);
        editor.PreviewSize.Should().Be(MaterialEditorViewModel.MinimumPreviewSize);
    }

    [Fact]
    public void DescribesTheMasterPicked()
    {
        _builder.WithMaterial("Masters/Surface.fvmaster", Master);
        MaterialEditorViewModel editor = _builder.Build();

        editor.SelectedNode = editor.Tree[0];

        editor.MasterSummary.Should().Contain("Painted").And.Contain("Grime");
        editor.SelectedMaterial.Should().BeNull();
        editor.CreateInstanceCommand.CanExecute(null).Should().BeTrue();
    }
}
