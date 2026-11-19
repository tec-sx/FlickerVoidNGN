using System.Numerics;

using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Rendering.Materials;

using NSubstitute;

namespace FlickerVoid.Engine.Editor.Tests.Materials;

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
    public void SavesAnEditAndTellsTheSceneTheMaterialChanged()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";
        List<string> changed = [];
        _builder.Scene.Content.Changed += changed.Add;

        NumberFieldViewModel specular = editor.Parameters.OfType<NumberFieldViewModel>().Single(field => field.Label == "Specular");
        specular.Value = 2;

        string saved = _builder.ReadMaterial("Brick.fvmat");
        saved.Should().Contain("\"specular\": 2");
        changed.Should().ContainSingle().Which.Should().EndWith("Brick.fvmat");
    }

    [Fact]
    public void PreviewsTheEditedMaterialOnTheChosenShape()
    {
        _builder.WithMaterial("Brick.fvmat", """{ "parameters": {} }""");
        MaterialEditorViewModel editor = _builder.Build();
        editor.SelectedMaterial = "Brick.fvmat";

        editor.Parameters.OfType<ColorFieldViewModel>().Single(field => field.Label == "Emissive").Color = Avalonia.Media.Colors.White;
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
