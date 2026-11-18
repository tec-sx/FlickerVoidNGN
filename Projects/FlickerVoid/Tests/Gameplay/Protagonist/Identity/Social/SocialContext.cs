using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social;

/// <summary>Condition contexts for the social conditions and effects: substitute entities, and services holding the given substitutes.</summary>
internal static class SocialContext
{
    public static ConditionContext Create(IFactDatabase facts, params (Type Type, object Service)[] services)
    {
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        foreach ((Type type, object service) in services)
        {
            provider.GetService(type).Returns(service);
        }

        return new ConditionContext(facts)
        {
            Instigator = Substitute.For<IEntityView>(),
            Target = Substitute.For<IEntityView>(),
            Services = provider,
        };
    }
}
