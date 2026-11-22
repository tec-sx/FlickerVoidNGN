using FlickerVoid.Engine.Attributes.Checks;
using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Rules;
using FlickerVoid.Engine.Attributes.Serialization;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Attributes;

/// <summary>Adds attributes to a game: the <c>attribute</c>, <c>modifier</c> and <c>check</c> data asset types, the attribute conditions and effects, the <c>attributes</c> component, and the attribute and check services.</summary>
public static class AttributesGameExtensions
{
    /// <summary>Adds attributes; <paramref name="random"/> rolls the checks, so tests can seed it.</summary>
    public static AttributeSystem AddAttributes(this Game game, Random? random = null)
    {
        game.Data.AddResolver(AttributesJsonContext.Default);
        game.Data.Register<AttributeDefinition>("attribute");
        game.Data.Register<ModifierDefinition>("modifier");
        game.Data.Register<CheckDefinition>("check");
        game.Data.RegisterDerived<Condition, AttributeCondition>("attribute");
        game.Data.RegisterDerived<Condition, PassiveCheckCondition>("passiveCheck");
        game.Data.RegisterDerived<Effect, ModifyAttributeEffect>("modifyAttribute");
        game.Data.RegisterDerived<Effect, SetAttributeEffect>("setAttribute");
        game.Data.RegisterDerived<Effect, ApplyModifierEffect>("applyModifier");
        game.Data.RegisterDerived<Effect, RemoveModifierEffect>("removeModifier");
        game.Spawner.Register("attributes", AttributesJsonContext.Default.AttributeSet);

        AttributeSystem attributes = new(game);
        game.Services.AddService(attributes);
        CheckSystem checks = new(attributes, random ?? Random.Shared);
        game.Services.AddService(checks);
        return attributes;
    }
}
