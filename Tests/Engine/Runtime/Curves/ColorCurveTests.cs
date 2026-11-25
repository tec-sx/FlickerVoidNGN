using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Runtime.Curves;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Curves;

public sealed class ColorCurveTests
{
    private const float Precision = 1e-4f;
    private const float Day = 24;

    [Fact]
    public void IsBlackWithoutKeys()
    {
        ColorCurve curve = new ColorCurveTestsBuilder().Build();

        curve.Evaluate(3, Day).Should().Be(Vector3.Zero);
    }

    [Fact]
    public void PassesThroughItsKeysAndHoldsItsEnds()
    {
        ColorCurve curve = new ColorCurveTestsBuilder().Key(1, 1, 0.5f, 0.25f).Key(2, 0, 0, 1).Key(5, 0.2f, 0.9f, 0).Build();

        foreach (ColorKey key in curve.Keys)
        {
            Vector3 color = curve.Evaluate(key.Time);
            Vector3.Distance(color, key.Color).Should().BeLessThan(Precision, $"the key at {key.Time} is on the curve");
        }

        curve.Evaluate(0).Should().Be(curve.Keys[0].Color);
        curve.Evaluate(9).Should().Be(curve.Keys[2].Color);
    }

    [Fact]
    public void BlendsEachChannelByTheKeysMode()
    {
        ColorCurve curve = new ColorCurveTestsBuilder()
            .Key(0, 0, 1, 0.5f, CurveMode.Linear)
            .Key(2, 1, 0, 0.5f, CurveMode.Constant)
            .Key(4, 0, 0, 0)
            .Build();

        Vector3 between = curve.Evaluate(0.5f);
        Vector3 held = curve.Evaluate(3.9f);

        Vector3 quarter = new(0.25f, 0.75f, 0.5f);
        Vector3.Distance(between, quarter).Should().BeLessThan(Precision);
        held.Should().Be(curve.Keys[1].Color);
    }

    [Fact]
    public void DoesNotOvershootAChannelBetweenSmoothKeys()
    {
        ColorCurve curve = new ColorCurveTestsBuilder().Key(0, 0, 1, 0).Key(1, 1, 1, 0).Key(2, 1, 0, 0).Key(2.2f, 0, 0, 1).Build();

        for (float time = 0; time <= 2.2f; time += 0.01f)
        {
            Vector3 color = curve.Evaluate(time);
            color.X.Should().BeInRange(-Precision, 1 + Precision);
            color.Y.Should().BeInRange(-Precision, 1 + Precision);
            color.Z.Should().BeInRange(-Precision, 1 + Precision);
        }
    }

    [Fact]
    public void WrapsAroundItsPeriod()
    {
        ColorCurve curve = new ColorCurveTestsBuilder().Key(6, 0, 0, 0, CurveMode.Linear).Key(18, 1, 0.5f, 0, CurveMode.Linear).Build();

        Vector3 midnight = curve.Evaluate(0, Day);
        Vector3 nextNoon = curve.Evaluate(36, Day);

        Vector3 half = new(0.5f, 0.25f, 0);
        Vector3.Distance(midnight, half).Should().BeLessThan(Precision);
        Vector3.Distance(nextNoon, half).Should().BeLessThan(Precision);
    }

    [Fact]
    public void ReadsAndWritesItsKeysAsJson()
    {
        const string json = """{"keys":[{"time":6,"color":[1,0.5,0.25]},{"time":18,"color":[0,0,1],"mode":"Constant"}]}""";

        ColorCurve? curve = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.ColorCurve);

        curve.Should().NotBeNull();
        Vector3 first = new(1, 0.5f, 0.25f);
        curve.Keys[0].Color.Should().Be(first);
        curve.Keys[1].Mode.Should().Be(CurveMode.Constant);
        string written = JsonSerializer.Serialize(curve, RuntimeJsonContext.Default.ColorCurve);
        written.Should().Be(json);
    }
}
