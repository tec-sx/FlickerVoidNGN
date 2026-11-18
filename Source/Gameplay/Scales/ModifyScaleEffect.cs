using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Gameplay.Scales;

/// <summary>Changes a scale within its range and passes the change on through its links.</summary>
public sealed class ModifyScaleEffect : Effect
{
    public required ScaleDefinition Scale { get; init; }

    public int Delta { get; init; }

    public override void Apply(ConditionContext context)
    {
        Scale.Modify(context.Facts, Delta);
    }

    public override string Describe() => $"{Scale.Display.Name} {Delta}";
}
