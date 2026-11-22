using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Gameplay.World.Objects.Doors;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

public sealed class ClickControlSystemTests : IDisposable
{
    private const string Hero = """
        { "name": "Hero", "components": { "transform": { "position": [4, 0, 0] }, "protagonist": {}, "characterMover": {}, "interactor": { "reach": 1.5 },
          "inventory": {} } }
        """;

    private const string LockedDoor = """
        { "type": "interactable", "offers": [ { "input": "Input.Use", "action": "Interaction.Action.Toggle", "display": { "name": "Open" } } ] }
        """;

    private const string Door = """
        { "name": "Door", "components": { "transform": {},
          "staticBody": { "collider": { "type": "box", "size": [1, 2, 0.1], "offset": [0.5, 1, 0] } },
          "interactable": { "definition": "Door.fvdata" },
          "lock": { "key": "Key.Cellar", "consumeKey": true },
          "toggle": { "action": "Interaction.Action.Toggle" },
          "doorSwing": {} } }
        """;

    private const string KeyPickup = """
        { "type": "interactable", "offers": [ { "input": "Input.Use", "action": "Interaction.Action.PickUp", "display": { "name": "Take" },
          "effects": { "effects": [ { "$type": "pickUp" } ] } } ] }
        """;

    private const string CellarKey = """{ "type": "item", "category": "Item.Key", "tags": [ "Key.Cellar" ] }""";

    private const string Key = """
        { "name": "Key", "components": { "transform": { "position": [2.5, 0, 2] },
          "staticBody": { "collider": { "type": "box", "size": [0.3, 0.1, 0.3], "offset": [0, 0.05, 0] } },
          "interactable": { "definition": "Pickup.fvdata" },
          "pickup": { "item": "CellarKey.fvdata" } } }
        """;

    private const string Talk = """
        { "type": "interactable", "offers": [ { "input": "Input.Use", "action": "Interaction.Action.Talk", "display": { "name": "Talk" },
          "effects": { "effects": [ { "$type": "startConversation" } ] } } ] }
        """;

    private const string Innkeeper = """
        { "name": "Innkeeper", "components": { "transform": { "position": [-2, 0, 0] },
          "staticBody": { "collider": { "type": "box", "size": [0.6, 1.7, 0.4], "offset": [0, 0.85, 0] } },
          "interactable": { "definition": "Talk.fvdata" },
          "dialogueParticipant": { "speaker": "Character.Innkeeper", "dialogue": "Innkeeper.fvflow" } } }
        """;

    private const string Quest = """
        { "type": "quest", "id": "Quest.Cellar", "objectives": [
          { "fact": "Quest.Cellar.FoundKey", "completeWhen": { "conditions": [ { "$type": "hasItem", "item": "CellarKey.fvdata" } ] } },
          { "fact": "Quest.Cellar.ReturnedKey" } ],
          "onCompleted": { "effects": [ { "$type": "giveMoney", "amount": 5 } ] } }
        """;

    private const string InnkeeperDialogue = """
        { "nodes": [
            { "$type": "start", "id": "start" },
            { "$type": "choice", "id": "ask", "options": [
              { "text": "Anything I can do?", "conditions": { "conditions": [ { "$type": "questState", "quest": "Quest.Cellar", "state": "NotStarted" } ] },
                "onChosen": { "effects": [ { "$type": "setQuestState", "quest": "Quest.Cellar", "state": "Active" } ] } },
              { "text": "I found your key.", "conditions": { "conditions": [ { "$type": "hasItem", "item": "CellarKey.fvdata" } ] } } ] },
            { "$type": "completeObjective", "id": "returned", "objective": "Quest.Cellar.ReturnedKey" },
            { "$type": "finish", "id": "end" } ],
          "links": [ { "from": "start", "to": "ask" }, { "from": "ask", "output": "First", "to": "end" },
            { "from": "ask", "output": "Second", "to": "returned" }, { "from": "returned", "to": "end" } ] }
        """;

    private readonly ClickControlSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AClickOnTheGroundWalksTheProtagonistThere()
    {
        _builder.Build();
        IReadOnlyList<Entity> spawned = _builder.Spawn(string.Join(", ", ClickControlSystemTestsBuilder.Camera, ClickControlSystemTestsBuilder.Floor, Hero));
        Vector3 spot = new(1, 0, 2);

        _builder.ClickAt(spawned[0], spot);
        _builder.Run(3);

        Vector3 position = _builder.Game.World.Get<Transform>(spawned[2]).Position;
        Vector3.Distance(position, spot).Should().BeLessThan(0.1f);
    }

    [Fact]
    public void AClickOnTheKeyPicksItUpAndAClickOnTheLockedDoorOpensItWithTheKey()
    {
        _builder.WithFile("Door.fvdata", LockedDoor).WithFile("Pickup.fvdata", KeyPickup).WithFile("CellarKey.fvdata", CellarKey).Build();
        string entities = string.Join(", ", ClickControlSystemTestsBuilder.Camera, ClickControlSystemTestsBuilder.Floor, Hero, Door, Key);
        IReadOnlyList<Entity> spawned = _builder.Spawn(entities);
        Entity camera = spawned[0];
        Entity hero = spawned[2];
        Entity door = spawned[3];
        Entity key = spawned[4];
        InventorySystem inventories = _builder.Game.Services.GetService<InventorySystem>()!;

        _builder.ClickAt(camera, new Vector3(2.5f, 0, 2));
        _builder.Run(2.5f);
        bool carried = inventories.FindItemWithTag(hero, new Tag("Key.Cellar")) is not null;
        _builder.ClickAt(camera, new Vector3(0.5f, 0, 0));
        _builder.Run(3);

        carried.Should().BeTrue();
        _builder.Game.World.IsAlive(key).Should().BeFalse();
        _builder.Game.World.Get<Lockable>(door).Locked.Should().BeFalse();
        _builder.Game.World.Get<Toggle>(door).Open.Should().BeTrue();
        inventories.GetStacks(hero).Should().BeEmpty("the lock consumes its key");
        Quaternion rotation = _builder.Game.World.Get<Transform>(door).Rotation;
        Vector3 across = Vector3.Transform(Vector3.UnitX, rotation);
        across.Z.Should().BeApproximately(-1, 1e-3f);
    }

    [Fact]
    public void WithoutTheKeyTheLockedDoorRattlesAndStaysShut()
    {
        _builder.WithFile("Door.fvdata", LockedDoor).Build();
        IReadOnlyList<Entity> spawned = _builder.Spawn(string.Join(", ", ClickControlSystemTestsBuilder.Camera, ClickControlSystemTestsBuilder.Floor, Hero, Door));
        Entity camera = spawned[0];
        Entity door = spawned[3];
        List<float> rattles = [];
        _builder.Game.Events.Handle((in ToggleBlockedEvent _) => rattles.Add(_builder.Game.World.Get<DoorHinge>(door).Rattle));

        _builder.ClickAt(camera, new Vector3(0.5f, 0, 0));
        _builder.Run(2.5f);

        Vector3 position = _builder.Game.World.Get<Transform>(spawned[2]).Position;
        position.X.Should().BeInRange(1.3f, 1.5f, "it stops within reach, its radius clear of the door's edge");
        _builder.Game.World.Get<Lockable>(door).Locked.Should().BeTrue();
        _builder.Game.World.Get<Toggle>(door).Open.Should().BeFalse();
        rattles.Should().ContainSingle().Which.Should().Be(DoorSwingSystem.RattleTime);
    }

    [Fact]
    public void TalkingToTheInnkeeperGivesTheQuestAndBringingTheKeyCompletesIt()
    {
        _builder.WithFile("Talk.fvdata", Talk).WithFile("Innkeeper.fvflow", InnkeeperDialogue).WithFile("Quests/Cellar.fvdata", Quest)
            .WithFile("Pickup.fvdata", KeyPickup).WithFile("CellarKey.fvdata", CellarKey).Build();
        string entities = string.Join(", ", ClickControlSystemTestsBuilder.Camera, ClickControlSystemTestsBuilder.Floor, Hero, Innkeeper, Key);
        IReadOnlyList<Entity> spawned = _builder.Spawn(entities);
        Entity camera = spawned[0];
        Entity hero = spawned[2];
        DialogueSystem dialogue = _builder.Game.Services.GetService<DialogueSystem>()!;
        QuestSystem quests = _builder.Game.Services.GetService<QuestSystem>()!;
        InventorySystem inventories = _builder.Game.Services.GetService<InventorySystem>()!;

        _builder.ClickAt(camera, new Vector3(-2, 0, 0));
        _builder.Run(6);
        bool talking = dialogue.IsInConversation;
        Vector3 before = _builder.Game.World.Get<Transform>(hero).Position;
        _builder.ClickAt(camera, new Vector3(3, 0, 3));
        _builder.Run(1);
        Vector3 after = _builder.Game.World.Get<Transform>(hero).Position;
        dialogue.Choose(0);
        QuestState given = quests.GetState(new Tag("Quest.Cellar"));
        _builder.ClickAt(camera, new Vector3(2.5f, 0, 2));
        _builder.Run(6);
        _builder.ClickAt(camera, new Vector3(-2, 0, 0));
        _builder.Run(6);
        dialogue.Choose(1);

        talking.Should().BeTrue();
        after.Should().Be(before);
        given.Should().Be(QuestState.Active);
        quests.GetState(new Tag("Quest.Cellar")).Should().Be(QuestState.Completed);
        inventories.GetMoney(hero).Should().Be(5);
        dialogue.IsInConversation.Should().BeFalse();
    }
}
