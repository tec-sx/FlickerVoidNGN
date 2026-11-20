using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Facts;

/// <summary>Builds the fact effect under test, on fact A.</summary>
internal sealed class FactEffectTestsBuilder
{
    private FactWrite _write = FactWrite.Set;
    private int _value = 1;

    public FactEffectTestsBuilder Writing(FactWrite write, int value)
    {
        _write = write;
        _value = value;
        return this;
    }

    public FactEffect Build() => new() { Fact = TestTags.A, Write = _write, Value = _value };
}
