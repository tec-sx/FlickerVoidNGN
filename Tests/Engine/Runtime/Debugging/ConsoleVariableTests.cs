using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Runtime.Tests.Debugging;

public class ConsoleVariableTests
{
    private enum Quality
    {
        Low,
        High,
    }

    [Fact]
    public void EnumVariablesTakeTheirNamesIgnoringCase()
    {
        ConsoleVariable<Quality> quality = ConsoleVariable.CreateEnum("r.Quality", "How good.", Quality.Low);

        string shown = quality.Execute("high");

        shown.Should().Be("r.Quality = High");
        quality.Value.Should().Be(Quality.High);
        quality.Choices.Should().Equal("Low", "High");
    }

    [Fact]
    public void EnumVariablesRejectNumbersAndUnknownNames()
    {
        ConsoleVariable<Quality> quality = ConsoleVariable.CreateEnum("r.Quality", "How good.", Quality.Low);

        bool number = quality.TrySet("1");
        string unknown = quality.Execute("ultra");

        number.Should().BeFalse();
        unknown.Should().Be("'ultra' is not one of Low, High for r.Quality.");
        quality.Value.Should().Be(Quality.Low);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("on", true)]
    [InlineData("True", true)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("false", false)]
    public void BooleansTakeOnesZerosAndWords(string text, bool expected)
    {
        ConsoleVariable<bool> rayTracing = ConsoleVariable.Create("r.RayTracing", "Traces rays.", !expected);

        bool set = rayTracing.TrySet(text);

        set.Should().BeTrue();
        rayTracing.Value.Should().Be(expected);
        rayTracing.Text.Should().Be(expected ? "true" : "false");
    }

    [Fact]
    public void KnowsWhetherItHoldsItsDefaultAndResetsToIt()
    {
        ConsoleVariable<float> scale = ConsoleVariable.Create("Time.Scale", "Game speed.", 1f);
        scale.Value = 0.5f;
        bool changed = !scale.IsDefault;

        scale.Reset();

        changed.Should().BeTrue();
        scale.Value.Should().Be(1);
        scale.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void NamesTheKindOfValueItHolds()
    {
        ConsoleVariable<bool> on = ConsoleVariable.Create("a", "", true);
        ConsoleVariable<int> count = ConsoleVariable.Create("b", "", 3);
        ConsoleVariable<string> text = ConsoleVariable.Create("c", "", "x");
        ConsoleVariable<Quality> quality = ConsoleVariable.CreateEnum("d", "", Quality.Low);

        ConsoleValueKind[] kinds = [on.Kind, count.Kind, text.Kind, quality.Kind];

        kinds.Should().Equal(ConsoleValueKind.Boolean, ConsoleValueKind.Number, ConsoleValueKind.Text, ConsoleValueKind.Text);
    }
}
