using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Editor.Overlays;

namespace FlickerVoid.Engine.Editor.Tests.Overlays;

public sealed class EditorIconsTests : IDisposable
{
    private const int Width = EditorIconsTestsBuilder.Width;
    private const int Height = EditorIconsTestsBuilder.Height;

    private readonly EditorIconsTestsBuilder _builder = new();
    private readonly EditorIcons _icons = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsPointLightsAsBulbsInTheirColour()
    {
        Vector3 color = new(1, 0.5f, 0.25f);
        Entity light = _builder.AddPointLight(new Vector3(1, 2, 3), color);

        _icons.Collect(_builder.World, null);

        _icons.Icons.Should().ContainSingle().Which.Should().Be(new IconInstance(light, new Vector3(1, 2, 3), EditorIcon.PointLight, new Vector4(color, 1)));
    }

    [Fact]
    public void ShowsEachKindOfEntityWithAnIconByItsOwnIcon()
    {
        _builder.AddSpotLight(Vector3.Zero);
        _builder.AddCamera(Vector3.One);
        _builder.AddIrradianceVolume(Vector3.UnitY);

        _icons.Collect(_builder.World, null);

        EditorIcon[] shown = [.. _icons.Icons.Select(static icon => icon.Icon)];
        shown.Should().BeEquivalentTo([EditorIcon.SpotLight, EditorIcon.Camera, EditorIcon.IrradianceVolume]);
    }

    [Fact]
    public void ShowsTheSelectedIconInTheSelectionColour()
    {
        Entity light = _builder.AddPointLight(Vector3.Zero, Vector3.One);

        _icons.Collect(_builder.World, light);

        _icons.Icons[0].Color.Should().Be(EditorIcons.SelectedColor);
    }

    [Fact]
    public void FindsTheIconUnderTheMouse()
    {
        Entity light = _builder.AddPointLight(new Vector3(1, 0, 0), Vector3.One);
        _builder.AddPointLight(new Vector3(-1, 0, 0), Vector3.One);
        _icons.Collect(_builder.World, null);
        Vector2 pixel = _builder.Camera.Project(new Vector3(1, 0, 0), Width, Height);
        Vector2 nearEdge = pixel + new Vector2((EditorIcons.SizePixels / 2) - 2, 0);

        Entity? hit = _icons.HitTest(_builder.Camera, nearEdge, Width, Height);

        hit.Should().Be(light);
    }

    [Fact]
    public void FindsNoIconAwayFromThem()
    {
        _builder.AddPointLight(Vector3.Zero, Vector3.One);
        _icons.Collect(_builder.World, null);
        Vector2 pixel = _builder.Camera.Project(Vector3.Zero, Width, Height);
        Vector2 outside = pixel + new Vector2(EditorIcons.SizePixels, 0);

        Entity? hit = _icons.HitTest(_builder.Camera, outside, Width, Height);

        hit.Should().BeNull();
    }

    [Fact]
    public void FindsTheNearestOfOverlappingIcons()
    {
        Vector3 toCamera = Vector3.Normalize(_builder.Camera.Position);
        _builder.AddPointLight(Vector3.Zero, Vector3.One);
        Entity near = _builder.AddPointLight(toCamera, Vector3.One);
        _icons.Collect(_builder.World, null);
        Vector2 pixel = _builder.Camera.Project(Vector3.Zero, Width, Height);

        Entity? hit = _icons.HitTest(_builder.Camera, pixel, Width, Height);

        hit.Should().Be(near);
    }
}
