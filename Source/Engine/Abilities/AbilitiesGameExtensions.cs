using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Abilities.Costs;
using FlickerVoid.Engine.Abilities.Definitions;
using FlickerVoid.Engine.Abilities.Rules;
using FlickerVoid.Engine.Abilities.Serialization;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Abilities;

/// <summary>Adds abilities to a game, after attributes: the <c>ability</c>, <c>abilitySet</c> and <c>abilityTagRelationships</c> data asset types, the costs, the ability rules, the <c>abilities</c> component and the ability service.</summary>
public static class AbilitiesGameExtensions
{
    public static AbilitySystem AddAbilities(this Game game)
    {
        game.Data.AddResolver(AbilitiesJsonContext.Default);
        game.Data.Register<AbilityDefinition>("ability");
        game.Data.Register<AbilitySetDefinition>("abilitySet");
        game.Data.Register<AbilityTagRelationshipsDefinition>("abilityTagRelationships");
        game.Data.RegisterDerived<AbilityCost, AttributeCost>("attributeCost");
        game.Data.RegisterDerived<Effect, ActivateAbilityEffect>("activateAbility");
        game.Data.RegisterDerived<Condition, CanActivateAbilityCondition>("canActivateAbility");
        game.Spawner.Register("abilities", AbilitiesJsonContext.Default.GrantedAbilities);

        AbilitySystem abilities = new(game);
        game.Services.AddService(abilities);
        return abilities;
    }
}
