using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

/// <summary>The player has earned the title, whether or not it is the current one.</summary>
public sealed class HasTitleCondition : Condition
{
    public required TitleDefinition Title { get; init; }

    public override string Describe() => $"Has title {Title.Display.Name}";

    protected override bool Evaluate(ConditionContext context) => Title.IsEarned(context.Facts);
}
