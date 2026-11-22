using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Story.Quests;

namespace FlickerVoid.Gameplay.Tests.Story;

/// <summary>
/// The stage 7 gate: the basic story loop of the Unreal milestone plays in the main scene, with interaction, dialogue, a quest,
/// the inventory and the map, and a save made along the way brings the game back as it was.
/// </summary>
public sealed class StoryLoopTests : IDisposable
{
    private static readonly Tag Cellar = new("Quest.Cellar");
    private static readonly Tag Key = new("Key.Cellar");

    /// <summary>The middle of the door's panel, from its hinge.</summary>
    private static readonly Vector3 Panel = new(0.45f, 0, 0);

    private readonly StoryLoopTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheCellarKeyQuestPlaysFromStartToEnd()
    {
        _builder.Start();
        Entity fox = _builder.Fox;
        MarkerHandle objective = _builder.Maps.FindMarker(_builder.Find("Door"));
        int money = _builder.Inventories.GetMoney(fox);
        bool objectiveShownBefore = _builder.Maps.IsVisible(objective);

        bool talking = _builder.Click("Innkeeper", () => _builder.Dialogue.IsInConversation);
        bool asked = _builder.Choose("Anything I can do");
        _builder.FinishConversation();
        QuestState accepted = _builder.Quests.GetState(Cellar);
        bool objectiveShown = _builder.Maps.IsVisible(objective);

        bool tookKey = _builder.Click("Cellar Key", () => _builder.Inventories.FindItemWithTag(fox, Key) is not null);
        bool opened = _builder.Click("Door", () => _builder.Game.World.Get<Toggle>(_builder.Find("Door")).Open, Panel);
        _builder.Click("Innkeeper", () => _builder.Dialogue.IsInConversation);
        bool told = _builder.Choose("I found your cellar key");
        _builder.FinishConversation();

        objectiveShownBefore.Should().BeFalse();
        talking.Should().BeTrue();
        asked.Should().BeTrue();
        accepted.Should().Be(QuestState.Active);
        objectiveShown.Should().BeTrue();
        tookKey.Should().BeTrue();
        opened.Should().BeTrue();
        _builder.Game.World.Get<Lockable>(_builder.Find("Door")).Locked.Should().BeFalse();
        told.Should().BeTrue();
        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Completed);
        _builder.Inventories.GetMoney(fox).Should().Be(money + 5);
    }

    [Fact]
    public void ASaveMadeOnTheWayBringsTheGameBackAfterARestart()
    {
        _builder.Start();
        _builder.Click("Innkeeper", () => _builder.Dialogue.IsInConversation);
        _builder.Choose("Anything I can do");
        _builder.FinishConversation();
        _builder.Click("Cellar Key", () => _builder.Inventories.FindItemWithTag(_builder.Fox, Key) is not null);
        bool saved = _builder.Saves.Save("Gate");
        _builder.Click("Door", () => _builder.Game.World.Get<Toggle>(_builder.Find("Door")).Open, Panel);

        _builder.Start();
        bool keyOnTheFloorAtStart = _builder.Find("Cellar Key") != Entity.Null;
        bool loaded = _builder.Saves.Load("Gate");
        _builder.Run(0.2f);

        saved.Should().BeTrue();
        keyOnTheFloorAtStart.Should().BeTrue();
        loaded.Should().BeTrue();
        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Active);
        _builder.Find("Cellar Key").Should().Be(Entity.Null, "it was picked up before the save");
        _builder.Inventories.FindItemWithTag(_builder.Fox, Key).Should().NotBeNull();
        Entity door = _builder.Find("Door");
        _builder.Game.World.Get<Lockable>(door).Locked.Should().BeTrue();
        _builder.Game.World.Get<Toggle>(door).Open.Should().BeFalse();
        bool opened = _builder.Click("Door", () => _builder.Game.World.Get<Toggle>(door).Open, Panel);
        opened.Should().BeTrue();
    }
}
