using Arch.Core;

using FlickerVoid.Engine.Abilities;
using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Ai;
using FlickerVoid.Engine.Ai.Paths;
using FlickerVoid.Engine.Ai.Senses;
using FlickerVoid.Engine.Attributes;
using FlickerVoid.Engine.Attributes.Checks;
using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Dialogue;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction;
using FlickerVoid.Engine.Maps;
using FlickerVoid.Engine.Maps.Navigation;
using FlickerVoid.Engine.Inventory;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Story;
using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Gameplay.Protagonist.Control;
using FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;
using FlickerVoid.Gameplay.Protagonist.Resources.Possessions;
using FlickerVoid.Gameplay.Serialization;
using FlickerVoid.Gameplay.World.Objects.Doors;
using FlickerVoid.Gameplay.World.Society.Residents;

namespace FlickerVoid.Gameplay;

/// <summary>
/// FlickerVoid's gameplay: physics, point and click characters, directed cameras, interaction, attributes and abilities, possessions, quests and knowledge for the protagonist,
/// conversations, doors that keys open and lockpicks pick, residents who wander, watch and listen, maps with their markers, and outlines on what the
/// protagonist can use nearby; the click control, the camera control and the scanner only where there is a window to click in.
/// </summary>
public sealed class GameplayModule : IGameModule
{
    public void Add(Game game)
    {
        IPhysicsSystem physics = game.AddPhysics();
        game.AddCharacters();
        CameraDirector cameras = game.AddCameras();
        PathSystem paths = game.AddAi();
        game.AddInteraction();
        game.AddAttributes();
        AbilitySystem abilities = game.AddAbilities();
        InventorySettings possessions = new() { KeyItemCategories = [new Tag("Item.Key")] };
        InventorySystem inventories = game.AddInventory(possessions);
        game.AddStory();
        DialogueSystem dialogue = game.AddDialogue();
        game.Spawner.Register("protagonist", FlickerVoidJsonContext.Default.Protagonist);
        AddPlayer(game);
        game.Spawner.Register("doorSwing", FlickerVoidJsonContext.Default.DoorSwing);
        game.Data.AddResolver(FlickerVoidJsonContext.Default);
        FlowNodeTypes.Register(game.Data);
        game.Data.Register<ScanDefinition>("scan");
        game.Spawner.Register("scanner", FlickerVoidJsonContext.Default.Scanner);
        game.Spawner.Register("scannable", FlickerVoidJsonContext.Default.Scannable);
        game.Spawner.Register("interactableHighlight", FlickerVoidJsonContext.Default.InteractableHighlight);

        // Keys hear a blocked door before lockpicks, and both before the door rattles.
        _ = new KeySystem(game.World, game.Events, inventories);
        CheckSystem checks = game.Services.GetService<CheckSystem>()!;
        _ = new LockpickSystem(game.World, game.Events, abilities, checks, game.Facts, game.Services);
        DoorSwingSystem doors = new(game);
        game.FixedSystems.Add(doors);
        InteractableHighlightSystem highlights = new(game.World);
        game.FrameSystems.Add(highlights);

        game.AddMaps(new MapSettings { WaypointMarker = "Markers/Waypoint.fvdata" });

        game.Spawner.Register("resident", FlickerVoidJsonContext.Default.Resident);
        PerceptionSystem perception = game.Services.GetService<PerceptionSystem>()!;
        ResidentSystem residents = new(game, paths, perception, dialogue, Random.Shared);
        game.FixedSystems.Add(residents);

        IGameWindow? window = game.Services.GetService<IGameWindow>();
        IInputState? input = game.Services.GetService<IInputState>();
        if (window is not null && input is not null)
        {
            PointerPicker picker = new(game.World, input, window, physics);
            game.Services.AddService(picker);
            _ = new ClickControlSystem(game.World, game.Events, picker, dialogue);
            AddScanner(game, physics, picker);
        }

        InputActions? actions = game.Services.GetService<InputActions>();
        if (actions is not null)
        {
            WalkControlSystem walk = new(game.World, game.Events, actions, dialogue);
            game.FrameSystems.Add(walk);
        }

        if (actions is not null && window is not null && input is not null)
        {
            CameraControlSystem cameraControl = new(game.World, actions, input, window, cameras);
            game.FrameSystems.Add(cameraControl);
        }
    }

    /// <summary>Adds the scanner, which focuses what the pointer is on.</summary>
    private static void AddScanner(Game game, IPhysicsSystem physics, PointerPicker picker)
    {
        ScannerSystem scanner = new(game, physics, picker);
        game.FixedSystems.Add(scanner);
        game.Services.AddService(scanner);
    }

    /// <summary>Makes the protagonist the player that quests and knowledge apply their rules for while it is active.</summary>
    private static void AddPlayer(Game game)
    {
        QuestSystem quests = game.Services.GetService<QuestSystem>()!;
        KnowledgeSystem knowledge = game.Services.GetService<KnowledgeSystem>()!;
        game.Lifecycle.OnActivate((Entity entity, ref Protagonist.Protagonist _) =>
        {
            WorldEntityView player = new(game.World, entity);
            quests.Player = player;
            knowledge.Player = player;
        });
        game.Lifecycle.OnDeactivate((Entity entity, ref Protagonist.Protagonist _) =>
        {
            quests.Player = null;
            knowledge.Player = null;
        });
    }
}
