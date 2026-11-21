using AwesomeAssertions;

using Xunit;

namespace FlickerVoid.Engine.Hosting.Tests;

public sealed class HostOptionsTests
{
    [Fact]
    public void ReadsTheSceneMoodFrameLimitSavesAndValidation()
    {
        HostOptions options = new HostOptionsTestsBuilder()
            .WithArguments("--scene", "Scenes/Main.fvscene", "--mood", "Moods/Day.fvmood", "--frames", "3", "--saves", "MySaves", "--validation")
            .Build();

        options.Scene.Should().Be("Scenes/Main.fvscene");
        options.Mood.Should().Be("Moods/Day.fvmood");
        options.FrameLimit.Should().Be(3);
        options.SaveFolder.Should().Be("MySaves");
        options.Validation.Should().BeTrue();
    }

    [Fact]
    public void DefaultsToTheStartupSceneAndTheContentBesideTheProgram()
    {
        HostOptions options = new HostOptionsTestsBuilder().WithStartupScene("Scenes/Main.fvscene").Build();

        options.Scene.Should().Be("Scenes/Main.fvscene");
        options.Mood.Should().BeNull();
        options.FrameLimit.Should().Be(0);
        options.Validation.Should().BeFalse();
        options.SaveFolder.Should().BeNull();
        string expected = Path.Combine(AppContext.BaseDirectory, "Content");
        options.ContentFolder.Should().Be(expected);
    }

    [Fact]
    public void RejectsAnUnknownOption()
    {
        HostOptionsTestsBuilder builder = new HostOptionsTestsBuilder().WithArguments("--fast");

        Action parse = () => builder.Build();

        parse.Should().Throw<ArgumentException>().WithMessage("*--fast*");
    }

    [Fact]
    public void RejectsAnOptionWithoutItsValue()
    {
        HostOptionsTestsBuilder builder = new HostOptionsTestsBuilder().WithArguments("--scene");

        Action parse = () => builder.Build();

        parse.Should().Throw<ArgumentException>().WithMessage("*--scene*");
    }
}
