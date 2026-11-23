using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneHierarchyTests : IDisposable
{
    private const string Stairs = """
        { "entities": [
          { "name": "Trigger", "components": { "transform": { "position": [0, 0, 0] } } },
          { "name": "Enter", "parent": "Trigger", "components": { "transform": { "position": [1, 0, 0] } } },
          { "name": "Exit", "parent": "Trigger", "components": { "transform": { "position": [-1, 0, 0] } } },
          { "name": "Handle", "parent": "Enter", "components": { "transform": { "position": [1, 1, 0] } } },
          { "name": "Lamp", "components": { "transform": { "position": [5, 0, 0] } } }
        ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    private World World => _builder.Scene.Game.World;

    private Entity Named(string name) => _builder.Scene.Find(name)!.Value;

    [Fact]
    public void ListsAnEntitysChildrenAndAllItsDescendants()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");

        IReadOnlyList<Entity> children = _builder.Scene.GetChildren(Named("Trigger"));
        IReadOnlyList<Entity> descendants = _builder.Scene.GetDescendants(Named("Trigger"));

        children.Should().Equal(Named("Enter"), Named("Exit"));
        descendants.Should().BeEquivalentTo([Named("Enter"), Named("Exit"), Named("Handle")]);
        _builder.Scene.GetParent(Named("Handle")).Should().Be(Named("Enter"));
        _builder.Scene.GetParent(Named("Lamp")).Should().BeNull();
    }

    [Fact]
    public void MovesChildrenWithTheirParentAsItIsEdited()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");

        Transform moved = new() { Position = new Vector3(0, 0, 3) };
        _builder.Scene.SetComponent(Named("Trigger"), moved);

        World.Get<Transform>(Named("Enter")).Position.Should().Be(new Vector3(1, 0, 3));
        World.Get<Transform>(Named("Handle")).Position.Should().Be(new Vector3(1, 1, 3));
    }

    [Fact]
    public void NestsAnEntityUnderAnotherAndUndoesIt()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");

        _builder.Scene.SetParent(Named("Lamp"), Named("Trigger")).Should().BeTrue();
        World.Get<EntityParent>(Named("Lamp")).Name.Should().Be("Trigger");
        _builder.Scene.History.Undo();

        World.Has<EntityParent>(Named("Lamp")).Should().BeFalse();
        _builder.Scene.History.Redo();
        _builder.Scene.GetParent(Named("Lamp")).Should().Be(Named("Trigger"));
    }

    [Fact]
    public void TakesAnEntityOffItsParent()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");

        _builder.Scene.SetParent(Named("Enter"), null).Should().BeTrue();
        Transform moved = new() { Position = new Vector3(0, 0, 3) };
        _builder.Scene.SetComponent(Named("Trigger"), moved);

        World.Has<EntityParent>(Named("Enter")).Should().BeFalse();
        World.Get<Transform>(Named("Enter")).Position.Should().Be(new Vector3(1, 0, 0));
    }

    [Fact]
    public void RefusesToNestAnEntityUnderItselfOrItsDescendants()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");

        _builder.Scene.SetParent(Named("Trigger"), Named("Trigger")).Should().BeFalse();
        _builder.Scene.SetParent(Named("Trigger"), Named("Handle")).Should().BeFalse();

        World.Has<EntityParent>(Named("Trigger")).Should().BeFalse();
    }

    [Fact]
    public void SavesTheParentsItWasGiven()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");

        _builder.Scene.SetParent(Named("Lamp"), Named("Exit"));
        _builder.Scene.Save();

        _builder.ReadScene("Stairs.fvscene").Should().Contain("\"name\":\"Lamp\",\"parent\":\"Exit\"");
    }

    [Fact]
    public void HidesAnEntityWithItsDescendantsWithoutLeavingTheSceneUnsaved()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");

        _builder.Scene.SetHidden([Named("Enter")], hidden: true);

        World.Has<Hidden>(Named("Enter")).Should().BeTrue();
        World.Has<Hidden>(Named("Handle")).Should().BeTrue();
        World.Has<Hidden>(Named("Exit")).Should().BeFalse();
        _builder.Scene.IsHidden(Named("Handle")).Should().BeTrue();
        _builder.Scene.IsUnsaved.Should().BeFalse();
    }

    [Fact]
    public void ShowsEveryHiddenEntityAgain()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");
        _builder.Scene.SetHidden([Named("Trigger"), Named("Lamp")], hidden: true);

        _builder.Scene.ShowAll();

        _builder.Scene.Entities.Should().NotContain(entity => World.Has<Hidden>(entity));
    }

    [Fact]
    public void LeavesHiddenEntitiesOutOfTheSavedScene()
    {
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");
        _builder.Scene.SetHidden([Named("Lamp")], hidden: true);
        Transform moved = new() { Position = new Vector3(6, 0, 0) };
        _builder.Scene.SetComponent(Named("Lamp"), moved);

        _builder.Scene.Save();

        _builder.ReadScene("Stairs.fvscene").Should().Contain("\"name\":\"Lamp\"").And.NotContain("hidden");
    }
}
