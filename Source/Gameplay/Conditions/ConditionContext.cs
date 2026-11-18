using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>What conditions are checked against and effects applied to.</summary>
public readonly record struct ConditionContext(FactDatabase Facts);
