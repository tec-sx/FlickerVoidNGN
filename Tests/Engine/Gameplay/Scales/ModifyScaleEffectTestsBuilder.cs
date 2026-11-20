using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Engine.Gameplay.Tests.Scales;

/// <summary>Builds the modify scale effect under test on the given scale.</summary>
internal sealed class ModifyScaleEffectTestsBuilder(ScaleDefinition scale)
{
    private int _delta;

    public ModifyScaleEffectTestsBuilder WithDelta(int delta)
    {
        _delta = delta;
        return this;
    }

    public ModifyScaleEffect Build() => new() { Scale = scale, Delta = _delta };
}
