using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Viewports;

using Avalonia.Input;

namespace FlickerVoid.Editor.Tests.Viewports;

public sealed class MeshToolsViewModelTests
{
    [Theory]
    [InlineData(Key.Tab, ViewportKey.Tab)]
    [InlineData(Key.D1, ViewportKey.One)]
    [InlineData(Key.D2, ViewportKey.Two)]
    [InlineData(Key.D3, ViewportKey.Three)]
    [InlineData(Key.I, ViewportKey.I)]
    [InlineData(Key.C, ViewportKey.C)]
    [InlineData(Key.B, ViewportKey.B)]
    public void TheMeshKeysAreMappedToTheViewport(Key key, ViewportKey expected)
    {
        ViewportKey? mapped = ViewportKeys.From(key);

        mapped.Should().Be(expected);
    }

    [Fact]
    public void WithoutACreatorTheBarIsHiddenAndOnlyTheDoorTestSpeaks()
    {
        List<string> messages = [];
        MeshToolsViewModel tools = new(() => null, messages.Add);

        tools.Extrude();
        tools.Test();

        tools.IsMeshMode.Should().BeFalse();
        tools.IsCreating.Should().BeFalse();
        tools.Mirror.Should().BeFalse();
        messages.Should().ContainSingle().Which.Should().Contain("door");
    }

    [Fact]
    public void TheTransformToolsKeepTheirTypedValuesAndStartNeutral()
    {
        MeshToolsViewModel tools = new(() => null, _ => { });

        tools.TransformToolIndex = 2;

        tools.TransformX.Should().Be(1);
        tools.TransformToolIndex = 0;
        tools.TransformX.Should().Be(0);
        tools.TransformY = 0.5;
        tools.TransformY.Should().Be(0.5);
    }
}
