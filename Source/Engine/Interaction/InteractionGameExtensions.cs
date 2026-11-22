using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Serialization;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Interaction;

/// <summary>
/// Adds interaction to a game: the <c>interactable</c> data asset type, the <c>interactorTags</c> and <c>locked</c> conditions, the
/// <c>interactable</c>, <c>interactor</c>, <c>lock</c> and <c>toggle</c> components, the systems that run them, and what saves keep of
/// interactables, locks and toggles.
/// </summary>
public static class InteractionGameExtensions
{
    public static InteractionSystem AddInteraction(this Game game)
    {
        game.Data.AddResolver(InteractionJsonContext.Default);
        game.Data.Register<InteractableDefinition>("interactable");
        game.Data.RegisterDerived<Condition, InteractorTagsCondition>("interactorTags");
        game.Data.RegisterDerived<Condition, LockedCondition>("locked");
        game.Spawner.Register("interactable", InteractionJsonContext.Default.Interactable);
        game.Spawner.Register("interactor", InteractionJsonContext.Default.Interactor);
        game.Spawner.Register("lock", InteractionJsonContext.Default.Lockable);
        game.Spawner.Register("toggle", InteractionJsonContext.Default.Toggle);
        game.SaveStates.AddComponent("lock", InteractionJsonContext.Default.Lockable);
        game.SaveStates.AddComponent("toggle", InteractionJsonContext.Default.Toggle);

        InteractionSystem interaction = new(game);
        game.FixedSystems.Add(interaction);
        game.Services.AddService(interaction);
        _ = new LockSystem(game.World, game.Events);
        _ = new ToggleSystem(game.World, game.Events);
        return interaction;
    }
}
