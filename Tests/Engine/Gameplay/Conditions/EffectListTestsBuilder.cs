using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

/// <summary>Builds the effect list under test.</summary>
internal sealed class EffectListTestsBuilder
{
    private readonly List<Effect> _effects = [];

    public EffectListTestsBuilder With(Effect effect)
    {
        _effects.Add(effect);
        return this;
    }

    public EffectList Build() => new() { Effects = [.. _effects] };
}
