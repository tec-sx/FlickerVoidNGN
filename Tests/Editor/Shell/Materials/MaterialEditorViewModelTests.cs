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
        NumberFieldViewModel wrap = editor.Parameters.OfType<NumberFieldViewModel>().Single(field => field.Label == "Wrap");
        wrap.Value.Should().Be(0.5);
        wrap.Maximum.Should().Be(1);
        editor.Parameters.Single(field => field.Label == "Tint").Should().BeOfType<ColorFieldViewModel>();
    }

    [Fact]
    public void HoldsAnEditUnsavedAndTellsTheSceneTheMaterialChanged()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";
        List<string> changed = [];
        _builder.Scene.Content.Changed += changed.Add;

        NumberFieldViewModel specular = editor.Parameters.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
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
        NumberFieldViewModel specular = editor.Parameters.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
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
        NumberFieldViewModel edited = editor.Parameters.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        edited.Value = 2;

        editor.DiscardCommand.Execute(null);

        editor.IsUnsaved.Should().BeFalse();
        NumberFieldViewModel shown = editor.Parameters.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
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

        NumberFieldViewModel shown = editor.Parameters.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        shown.Value.Should().Be(0.25);
    }

    [Fact]
    public void PreviewsTheEditedMaterialOnTheChosenShape()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";

        editor.Parameters.OfType<ColorFieldViewModel>().Single(field => field.Label == "Emissive").Color = Vector3.One;
        editor.IsCube = true;

        _builder.Preview.Received().Render(
            Arg.Is<MaterialFile>(material => material.Parameters.ContainsKey("emissive")),
            PreviewShape.Cube,
            Arg.Any<float>(),
            MaterialEditorViewModel.PreviewSize);
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
}
