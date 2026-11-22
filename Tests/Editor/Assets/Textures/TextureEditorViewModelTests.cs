using FlickerVoid.Editor.Assets.Textures;
using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Assets.Tests.Textures;

public sealed class TextureEditorViewModelTests : IDisposable
{
    private readonly TextureEditorViewModelTestsBuilder _builder = new();

    public void Dispose()
    {
        _builder.Dispose();
    }

    [Fact]
    public void OffersTheTexturesSettingsAsFields()
    {
        TextureEditorViewModel editor = _builder.WithImage("Wall.tga", 8, 4).Build("Wall.tga");

        editor.Fields.Select(static field => field.Label).Should().Contain(["Compression", "Max Size", "Tint", "Crop X", "Crop Width"]);
    }

    [Fact]
    public void HoldsEditsInTheSettingsBesideTheImageUntilSaved()
    {
        TextureEditorViewModel editor = _builder.WithImage("Wall.tga", 8, 4).Build("Wall.tga");
        NumberField(editor, "Max Size").Value = 4;

        _builder.Content.IsUnsaved("Wall.tga.fvtexture").Should().BeTrue();
        _builder.Exists("Wall.tga.fvtexture").Should().BeFalse();
        editor.SaveCommand.Execute(null);

        _builder.ReadFile("Wall.tga.fvtexture").Should().Contain("\"maxSize\": 4");
    }

    [Fact]
    public void SaysWhatTheBuildMakesOfTheImage()
    {
        TextureEditorViewModel editor = _builder.WithImage("Wall.tga", 8, 4).Build("Wall.tga");

        editor.Summary.Should().Be("8 × 4, BC7");
        NumberField(editor, "Max Size").Value = 4;
        editor.Summary.Should().Be("4 × 2, BC7");
    }

    [Fact]
    public void PreviewsTheCropAndTint()
    {
        _builder.WithImage("Wall.tga", 8, 4).WithFile("Wall.tga.fvtexture", """{ "cropX": 4, "tint": [1, 1, 0.5] }""");

        TextureEditorViewModel editor = _builder.Build("Wall.tga");

        (byte red, _, byte blue) = TextureEditorViewModelTestsBuilder.PixelAt(editor, 128, 128);
        red.Should().Be(0, "the red half is cropped off");
        blue.Should().BeInRange(126, 129, "the blue half is tinted by half");
    }

    [Fact]
    public void DiscardGoesBackToTheSavedSettings()
    {
        TextureEditorViewModel editor = _builder.WithImage("Wall.tga", 8, 4).Build("Wall.tga");
        NumberField(editor, "Max Size").Value = 4;

        editor.DiscardCommand.Execute(null);

        _builder.Content.IsUnsaved("Wall.tga.fvtexture").Should().BeFalse();
        editor.Summary.Should().Be("8 × 4, BC7");
    }

    [Fact]
    public void EditsACanvasAndPreviewsItRepeatedByItsTiling()
    {
        _builder.WithImage("Paper.tga", 4, 4).WithFile("Paper.fvcanvas", """{ "image": "Paper.tga", "tiling": 2 }""");

        TextureEditorViewModel editor = _builder.Build("Paper.fvcanvas");

        editor.Fields.Select(static field => field.Label).Should().Equal("Image", "Tiling", "Intensity", "Tint");
        TextureEditorViewModelTestsBuilder.PixelAt(editor, 10, 10).Should().Be(((byte)255, (byte)0, (byte)0));
        TextureEditorViewModelTestsBuilder.PixelAt(editor, 74, 10).Should().Be(((byte)0, (byte)0, (byte)255), "the image repeats twice across the preview's height, and so as often across its width");
        TextureEditorViewModelTestsBuilder.PixelAt(editor, 138, 10).Should().Be(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void PreviewsACanvasWithoutIntensityAsItsAverageColour()
    {
        _builder.WithImage("Paper.tga", 4, 4).WithFile("Paper.fvcanvas", """{ "image": "Paper.tga", "intensity": 0 }""");

        TextureEditorViewModel editor = _builder.Build("Paper.fvcanvas");

        TextureEditorViewModelTestsBuilder.PixelAt(editor, 10, 10).Should().Be(((byte)128, (byte)0, (byte)128), "half red and half blue average to purple");
    }

    private static NumberFieldViewModel NumberField(TextureEditorViewModel editor, string label)
    {
        return editor.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == label);
    }
}
