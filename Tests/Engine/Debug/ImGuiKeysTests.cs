using Hexa.NET.ImGui;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Debug.Tests;

public sealed class ImGuiKeysTests
{
    [Theory]
    [InlineData(Key.A, ImGuiKey.A)]
    [InlineData(Key.Number5, ImGuiKey.Key5)]
    [InlineData(Key.F1, ImGuiKey.F1)]
    [InlineData(Key.Enter, ImGuiKey.Enter)]
    [InlineData(Key.Backspace, ImGuiKey.Backspace)]
    [InlineData(Key.Left, ImGuiKey.LeftArrow)]
    [InlineData(Key.ControlLeft, ImGuiKey.LeftCtrl)]
    public void MapsTheKeysImGuiUses(Key key, ImGuiKey expected)
    {
        bool mapped = ImGuiKeys.TryMap(key, out ImGuiKey imGuiKey);

        mapped.Should().BeTrue();
        imGuiKey.Should().Be(expected);
    }

    [Fact]
    public void LeavesOtherKeysUnmapped()
    {
        bool mapped = ImGuiKeys.TryMap(Key.Unknown, out _);

        mapped.Should().BeFalse();
    }

    [Fact]
    public void MapsEveryLetterAndDigit()
    {
        for (Key key = Key.A; key <= Key.Z; key++)
        {
            ImGuiKeys.TryMap(key, out _).Should().BeTrue($"{key} is a letter");
        }

        for (Key key = Key.Number0; key <= Key.Number9; key++)
        {
            ImGuiKeys.TryMap(key, out _).Should().BeTrue($"{key} is a digit");
        }
    }
}
