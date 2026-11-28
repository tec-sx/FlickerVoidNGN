using Avalonia.Media;

using FlickerVoid.Editor.Controls;

namespace FlickerVoid.Editor.Tests.Controls;

public sealed class ThemeColorsTests
{
    [Fact]
    public void MixesInOklabAsTheDesignsDo()
    {
        Color bass = Color.Parse("#F5A639");
        Color panel = Color.Parse("#1D1610");
        Color green = Color.Parse("#56D1A3");

        ThemeColors.Mix(bass, panel, 0.2).Should().Be(Color.Parse("#422F1B"), "a clip's body is 20% of its colour over the panel");
        ThemeColors.Mix(green, panel, 0.78).Should().Be(Color.Parse("#4EA37F"), "a cue's tab is 78% of its colour over the panel");
    }

    [Fact]
    public void FadesAColourToAShareOfItsOpacity()
    {
        Color faded = ThemeColors.Fade(Colors.White, 0.5);

        faded.A.Should().Be(128);
        faded.R.Should().Be(255);
    }
}
