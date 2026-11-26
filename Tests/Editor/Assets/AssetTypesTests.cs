using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Tests;

public sealed class AssetTypesTests
{
    [Fact]
    public void GivesEachExtensionToOneType()
    {
        IEnumerable<string> extensions = AssetTypes.Files.SelectMany(static type => type.Extensions);

        extensions.Should().OnlyHaveUniqueItems(static extension => extension.ToLowerInvariant());
    }

    [Fact]
    public void WritesNewFilesOnlyOfTypesWithAnExtension()
    {
        IEnumerable<AssetType> made = AssetTypes.Files.Where(static type => type.New is not null);

        made.Should().OnlyContain(static type => type.Extensions.Count > 0);
    }

    [Fact]
    public void KnowsEnvironmentsAndLenses()
    {
        AssetTypes.Find("Street.fvenv").Should().BeSameAs(AssetTypes.Environment);
        AssetTypes.Find("Cameras/Main.fvlens").Should().BeSameAs(AssetTypes.Lens);
    }

    [Fact]
    public void WritesANewEnvironmentAndLensTheGameCanRead()
    {
        string environment = AssetTypes.Environment.New!.Write(".schemas/environment.schema.json");
        string lens = AssetTypes.Lens.New!.Write(".schemas/lens.schema.json");

        LightingEnvironment? readEnvironment = System.Text.Json.JsonSerializer.Deserialize(environment, RenderingJsonContext.Default.LightingEnvironment);
        CameraLens? readLens = System.Text.Json.JsonSerializer.Deserialize(lens, RenderingJsonContext.Default.CameraLens);

        readEnvironment!.Numbers.Should().BeEmpty();
        readLens.Should().NotBeNull();
        AssetTypes.Environment.New.Schema.Should().Be("environment");
        AssetTypes.Lens.New!.Schema.Should().Be("lens");
    }
}
