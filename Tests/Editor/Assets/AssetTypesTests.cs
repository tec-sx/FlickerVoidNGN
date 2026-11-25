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
}
