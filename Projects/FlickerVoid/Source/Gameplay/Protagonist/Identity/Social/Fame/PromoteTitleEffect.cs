using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

/// <summary>Makes a title the player's current one, earning it first if needed. It stays current until another title takes the lead.</summary>
public sealed class PromoteTitleEffect : Effect
{
    public required TitleDefinition Title { get; init; }

    public override void Apply(ConditionContext context)
    {
        ITitles? titles = context.GetService<ITitles>();
        titles?.PromoteTitle(Title);
    }

    public override string Describe() => $"Promote title {Title.Display.Name}";
}
