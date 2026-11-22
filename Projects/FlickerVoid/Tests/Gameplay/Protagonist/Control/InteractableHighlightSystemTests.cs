using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

public sealed class InteractableHighlightSystemTests : IDisposable
{
    private const string Fox = """
        { "name": "Fox", "components": { "transform": {}, "protagonist": {}, "interactor": { "kind": "Interactor.Player" },
          "interactableHighlight": { "range": 3, "color": [1, 0.5, 0] } } }
        """;

    private readonly InteractableHighlightSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void OutlinesInteractablesInRangeInTheHighlightColor()
    {
        _builder.Build();
        Entity lever = _builder.SpawnInteractable(new Vector3(2, 0, 0));
        _builder.Spawn(Fox);

        _builder.Run();

        _builder.Game.World.TryGet(lever, out Outline outline).Should().BeTrue();
        outline.Color.Should().Be(new Vector3(1, 0.5f, 0));
    }

    [Fact]
    public void OutlinesInteractablesOnlyOnceTheProtagonistComesNear()
    {
        _builder.Build();
        Entity lever = _builder.SpawnInteractable(new Vector3(5, 0, 0));
        Entity fox = _builder.Spawn(Fox);

        _builder.Run();
        bool farOutlined = _builder.Game.World.Has<Outline>(lever);
        _builder.Game.World.Get<Transform>(fox).Position = new Vector3(3, 0, 0);
        _builder.Run();

        farOutlined.Should().BeFalse();
        _builder.Game.World.Has<Outline>(lever).Should().BeTrue();
    }

    [Fact]
    public void TakesTheOutlineOffWhenTheProtagonistWalksAway()
    {
        _builder.Build();
        Entity lever = _builder.SpawnInteractable(new Vector3(1, 0, 0));
        Entity fox = _builder.Spawn(Fox);
        _builder.Run();

        _builder.Game.World.Get<Transform>(fox).Position = new Vector3(-5, 0, 0);
        _builder.Run();

        _builder.Game.World.Has<Outline>(lever).Should().BeFalse();
    }

    [Fact]
    public void LeavesInteractablesThatCantBeUsedUnlined()
    {
        _builder.Build();
        Entity lever = _builder.SpawnInteractable(new Vector3(1, 0, 0));
        Entity robotOnly = _builder.SpawnInteractable(new Vector3(0, 0, 1), "Interactor.Robot");
        _builder.Spawn(Fox);
        _builder.Run();

        _builder.Game.World.Get<ActiveInteractable>(lever).State = InteractableState.Completed;
        _builder.Run();

        _builder.Game.World.Has<Outline>(lever).Should().BeFalse("a used up interactable can't be used again");
        _builder.Game.World.Has<Outline>(robotOnly).Should().BeFalse("the protagonist isn't an interactor it accepts");
    }
}
