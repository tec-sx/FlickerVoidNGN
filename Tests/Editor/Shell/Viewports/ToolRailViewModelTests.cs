using FlickerVoid.Editor.Core.Gizmos;

namespace FlickerVoid.Editor.Tests.Viewports;

public sealed class ToolRailViewModelTests : IDisposable
{
    private readonly ViewportViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheLevelShowsOneGroupOfTheFourTransformToolsWithTheGizmoToolLit()
    {
        FlickerVoid.Editor.Viewports.ToolRailViewModel rail = _builder.ViewModel.ToolRail;

        rail.Groups.Should().ContainSingle();
        rail.Groups[0].Items.Select(item => item.Name).Should().Equal("Select", "Move", "Rotate", "Scale");
        rail.Groups[0].Items.Where(item => item.IsActive).Select(item => item.Name).Should().Equal("Move");
    }

    [Fact]
    public void PressingAKeyOrAToolLightsItAndOnlyIt()
    {
        FlickerVoid.Editor.Viewports.ToolRailViewModel rail = _builder.ViewModel.ToolRail;

        _builder.ViewModel.KeyPressed(FlickerVoid.Editor.Core.Cameras.ViewportKey.E, FlickerVoid.Editor.Core.Cameras.ViewportButtons.None);
        bool rotateByKey = rail.Groups[0].Items[2].IsActive;
        rail.Groups[0].Items[3].Command.Execute(null);

        rotateByKey.Should().BeTrue();
        _builder.ViewModel.GizmoMode.Should().Be(GizmoMode.Scale);
        rail.Groups[0].Items.Where(item => item.IsActive).Select(item => item.Name).Should().Equal("Scale");
    }

    [Fact]
    public void TheToolTipNamesTheKey()
    {
        FlickerVoid.Editor.Viewports.ToolRailViewModel rail = _builder.ViewModel.ToolRail;

        rail.Groups[0].Items[1].Tip.Should().Be("Move (W)");
    }

    [Fact]
    public void ACreatorDrawsOnADarkBackgroundWithNoSky()
    {
        FlickerVoid.Engine.Rendering.Lighting.LightingMood workshop = FlickerVoid.Editor.Viewports.ViewportViewModel.GetWorkshopMood();

        workshop.SunIntensity.Should().Be(0);
        workshop.StarIntensity.Should().Be(0);
        workshop.SkyZenith.Length().Should().BeLessThan(0.05f);
    }
}
