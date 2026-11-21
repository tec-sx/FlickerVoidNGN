using Arch.Core;

using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Dialogue;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction;
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
using FlickerVoid.Gameplay.Protagonist.Resources.Possessions;
using FlickerVoid.Gameplay.Serialization;
using FlickerVoid.Gameplay.World.Objects.Doors;

namespace FlickerVoid.Gameplay;

/// <summary>
/// FlickerVoid's gameplay: physics, point and click characters, interaction, possessions, quests and knowledge for the protagonist,
/// conversations, and doors that keys open; the click control only where there is a window to click in.
/// </summary>
public sealed class GameplayModule : IGameModule
{
    public void Add(Game game)
    {
        IPhysicsSystem physics = game.AddPhysics();
        game.AddCharacters();
        game.AddInteraction();
        InventorySettings possessions = new() { KeyItemCategories = [new Tag("Item.Key")] };
        InventorySystem inventories = game.AddInventory(possessions);
        game.AddStory();
        DialogueSystem dialogue = game.AddDialogue();
        game.Spawner.Register("protagonist", FlickerVoidJsonContext.Default.Protagonist);
        AddPlayer(game);
        game.Spawner.Register("doorSwing", FlickerVoidJsonContext.Default.DoorSwing);

        // Keys hear a blocked door before the door rattles.
        _ = new KeySystem(game.World, game.Events, inventories);
        DoorSwingSystem doors = new(game);
        game.FixedSystems.Add(doors);

        IGameWindow? window = game.Services.GetService<IGameWindow>();
        IInputState? input = game.Services.GetService<IInputState>();
        if (window is not null && input is not null)
        {
            _ = new ClickControlSystem(game.World, game.Events, input, window, physics, dialogue);
        }
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
