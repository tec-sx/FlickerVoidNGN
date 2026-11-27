using Avalonia.Controls;

using FlickerVoid.Editor.Core.Tags;
using FlickerVoid.Editor.Tags;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Editor.Tests.Tags;

public sealed class TagViewsTests
{
    private readonly TagViewsTestsBuilder _builder = new();

    [Fact]
    public async Task ShowsEachTagAsAChipThatRemovesItAndAnAddChip()
    {
        await _builder.Run(
            () => new TagChipsView { DataContext = _builder.Field },
            () =>
            {
                Border chip = _builder.Find<Border>().Single(border => border.Classes.Contains("tagChip"));
                chip.Bounds.Height.Should().Be(20);
                _builder.Find<TextBlock>().Select(text => text.Text).Should().Contain("Music.Drums").And.Contain("Tag");

                Button remove = _builder.Find<Button>().Single(button => button.Classes.Contains("tagRemove"));
                _builder.Click(remove);

                _builder.Value.Should().BeEmpty();
                _builder.Find<Border>().Should().NotContain(border => border.Classes.Contains("tagChip"));
                _builder.Find<Button>().Should().Contain(button => button.Classes.Contains("tagAdd"));
            });
    }

    [Fact]
    public async Task PicksATagWithAClickOnItsRowAndMarksItsParent()
    {
        TagPickerViewModel? picker = null;
        await _builder.Run(
            () =>
            {
                picker = _builder.Field.OpenPicker(_builder.Catalog);
                return new TagPickerView { DataContext = picker };
            },
            () =>
            {
                _builder.Find<TextBlock>().Select(text => text.Text).Should().Contain("1 picked").And.Contain("Hats").And.Contain("Ambience");
                _builder.Find<Border>().Count(border => border.Classes.Contains("tagRow")).Should().Be(5, "Ambience is folded: Ambience, Music, Drums, Inn, Hats");

                _builder.Find<StackPanel>().Should().NotContain(panel => panel.Classes.Contains("tagActions"), "a row shows add, rename and remove only while hovered");

                TextBlock hats = _builder.Find<TextBlock>().First(text => text.Text == "Hats");
                _builder.Click(hats);

                _builder.Value.Should().Equal(new Tag("Music.Drums"), new Tag("Music.Hats"));
                _builder.Find<Border>().Count(border => border.Classes.Contains("tagCheck") && border.Classes.Contains("on")).Should().Be(2);
                _builder.Find<Border>().Count(border => border.Classes.Contains("tagCheck") && border.Classes.Contains("part")).Should().Be(1);
                _builder.Find<Border>().First(border => border.Classes.Contains("tagRow")).Bounds.Height.Should().Be(23);
            });
    }
}
