using Avalonia.Media;

using FlickerVoid.Editor.Assets;

namespace FlickerVoid.Editor.Tests.Assets;

public sealed class AssetTypeLooksTests
{
    private readonly AssetTypeLooksTestsBuilder _builder = new();

    [Fact]
    public void GivesEveryTypeAnIconAndAColourOfItsOwn()
    {
        _builder.Run(static () =>
        {
            IEnumerable<AssetType> types = AssetTypes.Files.Append(AssetTypes.Folder);
            foreach (AssetType type in types)
            {
                LucideIcons.Find(type.Icon).Should().NotBeNull($"{type.Name} names the icon {type.Icon}");
                Color.TryParse(type.Color, out _).Should().BeTrue($"{type.Name}'s colour is {type.Color}");
            }

            types.Should().OnlyHaveUniqueItems(static type => type.Color);
            LucideIcons.Find(AssetTypes.Vehicle.Icon).Should().NotBeSameAs(LucideIcons.File);
        });
    }
}
