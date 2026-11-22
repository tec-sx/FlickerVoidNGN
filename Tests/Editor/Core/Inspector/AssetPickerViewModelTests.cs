using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Core.Tests.Inspector;

public sealed class AssetPickerViewModelTests
{
    private readonly AssetPickerViewModelTestsBuilder _builder = new();

    [Fact]
    public void ListsTheAssetsWhenOpened()
    {
        AssetPickerViewModel picker = _builder.WithAssets("Props/Crate.glb", "Room.glb").Build();
        _builder.WithAssets("Fox.glb");

        picker.IsOpen = true;

        picker.Matches.Should().Equal("Props/Crate.glb", "Room.glb", "Fox.glb");
    }

    [Fact]
    public void SearchKeepsTheAssetsWhosePathHoldsTheText()
    {
        AssetPickerViewModel picker = _builder.WithAssets("Props/Crate.glb", "Room.glb", "Props/Barrel.glb").Build();
        picker.IsOpen = true;

        picker.Search = "props/";

        picker.Matches.Should().Equal("Props/Crate.glb", "Props/Barrel.glb");
    }

    [Fact]
    public void ChoosingAnAssetMakesItCurrentAndCloses()
    {
        AssetPickerViewModel picker = _builder.WithAssets("Props/Crate.glb").Build();
        picker.IsOpen = true;
        picker.Search = "crate";

        picker.ChooseCommand.Execute("Props/Crate.glb");

        _builder.Chosen.Should().Equal("Props/Crate.glb");
        picker.Current.Should().Be("Props/Crate.glb");
        picker.Name.Should().Be("Crate.glb");
        picker.IsOpen.Should().BeFalse();
        picker.Search.Should().BeEmpty();
    }

    [Fact]
    public void ChoosesNoneOnlyWhenTheValueMayBeEmpty()
    {
        AssetPickerViewModel required = _builder.Build();
        AssetPickerViewModel optional = new AssetPickerViewModelTestsBuilder().AcceptingNone().Build();

        required.ChooseNoneCommand.CanExecute(null).Should().BeFalse();
        optional.ChooseNoneCommand.Execute(null);

        optional.Current.Should().BeNull();
        optional.Name.Should().Be("None");
    }
}
