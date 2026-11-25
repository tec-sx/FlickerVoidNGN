using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Core.Tests.Creators;

public sealed class CreatorModeTests : IDisposable
{
    private const int Width = CreatorModeTestsBuilder.Width;
    private const int Height = CreatorModeTestsBuilder.Height;

    private readonly CreatorModeTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void EnteringInPlaceGreysEverythingButTheCreation()
    {
        Entity van = _builder.Find("Van");
        Entity other = _builder.Find("Van 2");
        Entity crate = _builder.Find("Crate");

        using CreatorMode mode = _builder.EnterInPlace("Van");

        World world = _builder.World;
        mode.Root.Should().Be(van);
        mode.Placement.Should().Be(CreatorPlacement.InPlace);
        world.Has<Greyed>(crate).Should().BeTrue();
        _builder.Parts.FindAll(other).Should().OnlyContain(part => world.Has<Greyed>(part));
        _builder.Parts.FindAll(van).Should().OnlyContain(part => !world.Has<Greyed>(part) && !world.Has<Undrawn>(part));
        world.Has<Undrawn>(crate).Should().BeFalse();
    }

    [Fact]
    public void EnteringAloneSetsTheLevelAsideAroundATemporaryCopyThatIsNeverSaved()
    {
        Entity crate = _builder.Find("Crate");
        Entity other = _builder.Find("Van 2");

        using CreatorMode mode = _builder.EnterAlone();

        World world = _builder.World;
        mode.Placement.Should().Be(CreatorPlacement.Alone);
        world.Get<Vehicle>(mode.Root).File.Should().Be(CreatorModeTestsBuilder.File);
        _builder.Parts.FindAll(mode.Root).Should().HaveCount(5).And.OnlyContain(part => !world.Has<Undrawn>(part));
        world.Has<Undrawn>(crate).Should().BeTrue();
        _builder.Parts.FindAll(other).Should().OnlyContain(part => world.Has<Undrawn>(part));
        _builder.Scene.Entities.Should().NotContain(mode.Root).And.HaveCount(3);
        _builder.Scene.IsUnsaved.Should().BeFalse();
    }

    [Fact]
    public void PicksOnlyTheCreationsParts()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");
        Entity van = _builder.Find("Van");
        Entity wheel = _builder.Parts.Find(van, "wheel.1L");
        Vector3 wheelCenter = _builder.World.Get<Transform>(wheel).Position;
        Vector2 onWheel = _builder.PixelOf(wheelCenter);
        Vector2 onCrate = _builder.PixelOf(new Vector3(-4, 0.25f, 0));
        Ray toCrate = _builder.Camera.GetRay(onCrate, Width, Height);
        Entity? crate = _builder.Scene.Picker.Pick(_builder.World, toCrate);
        _builder.Scene.Selection.Select(van);

        mode.PointerPressed(onWheel, Width, Height);
        string? picked = mode.SelectedPart;
        mode.PointerPressed(onCrate, Width, Height);

        crate.Should().Be(_builder.Find("Crate"));
        picked.Should().Be("wheel.1L");
        mode.SelectedPart.Should().BeNull();
        _builder.Scene.Selection.Selected.Should().Be(van);
    }

    [Fact]
    public void LeavingClearsTheMarksTheCopyAndTheWorkLight()
    {
        CreatorMode inPlace = _builder.EnterInPlace("Van");
        Entity inPlaceLight = inPlace.WorkLight;
        inPlace.Dispose();
        bool inPlaceLightGone = !_builder.World.IsAlive(inPlaceLight);
        int greyedAfterInPlace = CountWith<Greyed>(_builder.World);
        CreatorMode alone = _builder.EnterAlone();
        Entity copy = alone.Root;
        IReadOnlyList<Entity> copyParts = _builder.Parts.FindAll(copy);
        Entity light = alone.WorkLight;

        alone.Dispose();

        World world = _builder.World;
        CountWith<Greyed>(world).Should().Be(0);
        CountWith<Undrawn>(world).Should().Be(0);
        world.IsAlive(copy).Should().BeFalse();
        copyParts.Should().OnlyContain(part => !world.IsAlive(part));
        world.IsAlive(light).Should().BeFalse();
        inPlaceLightGone.Should().BeTrue();
        greyedAfterInPlace.Should().Be(0);
        world.IsAlive(_builder.Find("Van")).Should().BeTrue();
    }

    [Fact]
    public void KeepsAWorkLightAtTheView()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");
        _builder.Camera.LookAt(new Vector3(1, 0, 2), yaw: -1.2f, pitch: -0.3f, distance: 6);

        mode.Update();

        World world = _builder.World;
        world.Has<PointLight>(mode.WorkLight).Should().BeTrue();
        Vector3 position = world.Get<Transform>(mode.WorkLight).Position;
        Vector3.Distance(position, _builder.Camera.Position).Should().BeLessThan(1e-4f);
        world.Get<PointLight>(mode.WorkLight).Range.Should().BeGreaterThan(_builder.Camera.PivotDistance);
        _builder.Scene.Entities.Should().NotContain(mode.WorkLight);
    }

    [Fact]
    public void UndoesAndSavesTheCreationsFile()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");

        bool saved = mode.Save();

        saved.Should().BeTrue();
        _builder.Saves.Should().Be(1);
        mode.History.Should().BeSameAs(_builder.FileHistory);
        mode.History.Should().NotBeSameAs(_builder.Scene.History);
    }

    [Fact]
    public void GreysThePartsTheFileGivesTheOtherCopiesWhileOpen()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");
        Entity van = _builder.Find("Van");
        Entity other = _builder.Find("Van 2");

        _builder.WriteWheels(6);

        World world = _builder.World;
        _builder.Parts.FindAll(other).Should().HaveCount(7).And.OnlyContain(part => world.Has<Greyed>(part));
        _builder.Parts.FindAll(van).Should().HaveCount(7).And.OnlyContain(part => !world.Has<Greyed>(part));
    }

    private static int CountWith<T>(World world)
    {
        QueryDescription query = new QueryDescription().WithAll<T>();
        return world.CountEntities(in query);
    }
}
