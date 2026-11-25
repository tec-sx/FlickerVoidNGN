using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Curves;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Curves;

public sealed class CurveTests
{
    private const float Precision = 1e-4f;
    private const float Day = 24;

    [Fact]
    public void IsZeroWithoutKeys()
    {
        Curve curve = new CurveTestsBuilder().Build();

        curve.Evaluate(3).Should().Be(0);
        curve.Evaluate(3, Day).Should().Be(0);
    }

    [Fact]
    public void HoldsItsOnlyKey()
    {
        Curve curve = new CurveTestsBuilder().Key(6, 2.5f).Build();

        curve.Evaluate(-1).Should().Be(2.5f);
        curve.Evaluate(40).Should().Be(2.5f);
        curve.Evaluate(40, Day).Should().Be(2.5f);
    }

    [Fact]
    public void PassesThroughItsKeys()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 1).Key(2, -3).Key(3, 4, CurveMode.Linear).Key(7, 0.5f).Build();

        foreach (CurveKey key in curve.Keys)
        {
            curve.Evaluate(key.Time).Should().BeApproximately(key.Value, Precision, $"the key at {key.Time} is on the curve");
        }
    }

    [Fact]
    public void HoldsItsEndsOutsideTheKeys()
    {
        Curve curve = new CurveTestsBuilder().Key(1, 2).Key(3, 6).Build();

        curve.Evaluate(-5).Should().Be(2);
        curve.Evaluate(9).Should().Be(6);
    }

    [Fact]
    public void IsStraightAfterALinearKey()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 0, CurveMode.Linear).Key(4, 8, CurveMode.Linear).Key(6, 0).Build();

        curve.Evaluate(1).Should().BeApproximately(2, Precision);
        curve.Evaluate(3).Should().BeApproximately(6, Precision);
        curve.Evaluate(5).Should().BeApproximately(4, Precision);
    }

    [Fact]
    public void HoldsAConstantKeyUntilTheNext()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 3, CurveMode.Constant).Key(2, 7).Build();

        curve.Evaluate(1).Should().Be(3);
        curve.Evaluate(1.999f).Should().Be(3);
        curve.Evaluate(2).Should().Be(7);
    }

    [Fact]
    public void StaysOnALineThroughSmoothKeys()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 0).Key(1, 1).Key(3, 3).Build();

        curve.Evaluate(0.5f).Should().BeApproximately(0.5f, Precision);
        curve.Evaluate(2.25f).Should().BeApproximately(2.25f, Precision);
    }

    [Fact]
    public void DoesNotOvershootBetweenSmoothKeys()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 0).Key(1, 1).Key(2, 1).Key(2.2f, 0).Key(6, 0.1f).Build();

        for (float time = 0; time <= 6; time += 0.01f)
        {
            float value = curve.Evaluate(time);
            value.Should().BeInRange(-Precision, 1 + Precision, $"the keys around {time} stay between 0 and 1");
        }

        curve.Evaluate(1.5f).Should().BeApproximately(1, Precision, "two equal keys hold their value between them");
    }

    [Fact]
    public void IsFlatAtASmoothPeak()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 0).Key(1, 1).Key(2, 0).Build();

        curve.Evaluate(0.99f).Should().BeGreaterThan(0.999f);
        curve.Evaluate(1.01f).Should().BeGreaterThan(0.999f);
    }

    [Fact]
    public void LeavesAndArrivesAtFreeTangents()
    {
        Curve eased = new CurveTestsBuilder().FreeKey(0, 0, 0, 0).FreeKey(1, 1, 0, 0).Build();
        Curve arched = new CurveTestsBuilder().FreeKey(0, 0, 0, 2).FreeKey(2, 0, -2, 0).Build();

        eased.Evaluate(0.25f).Should().BeApproximately(0.15625f, Precision);
        arched.Evaluate(1).Should().BeApproximately(1, Precision, "a tangent is a slope in value per time, whatever the keys' spacing");
    }

    [Fact]
    public void ArrivesStraightAtALinearKey()
    {
        Curve curve = new CurveTestsBuilder().FreeKey(0, 0, 0, 0).Key(1, 1, CurveMode.Linear).Key(2, 1).Build();

        curve.Evaluate(0.5f).Should().BeApproximately(0.375f, Precision);
    }

    [Fact]
    public void WrapsAroundItsPeriod()
    {
        Curve curve = new CurveTestsBuilder().Key(6, 0, CurveMode.Linear).Key(18, 10, CurveMode.Linear).Build();

        curve.Evaluate(12, Day).Should().BeApproximately(5, Precision);
        curve.Evaluate(21, Day).Should().BeApproximately(7.5f, Precision);
        curve.Evaluate(0, Day).Should().BeApproximately(5, Precision);
        curve.Evaluate(3, Day).Should().BeApproximately(2.5f, Precision);
        curve.Evaluate(36, Day).Should().BeApproximately(5, Precision);
        curve.Evaluate(-6, Day).Should().BeApproximately(10, Precision);
    }

    [Fact]
    public void IsSmoothAcrossItsPeriod()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 0).Key(12, 1).Build();

        curve.Evaluate(6, Day).Should().BeApproximately(0.5f, Precision);
        curve.Evaluate(23.9f, Day).Should().BeApproximately(curve.Evaluate(0.1f, Day), Precision);
        curve.Evaluate(23.9f, Day).Should().BeLessThan(0.001f, "midnight is the curve's lowest key, so it is flat there");
    }

    [Fact]
    public void SortsItsKeysByTime()
    {
        Curve curve = new CurveTestsBuilder().Key(4, 8, CurveMode.Linear).Key(0, 0, CurveMode.Linear).Key(2, 2, CurveMode.Linear).Build();

        curve.Keys.Select(key => key.Time).Should().Equal(0, 2, 4);
        curve.Evaluate(3).Should().BeApproximately(5, Precision);
    }

    [Fact]
    public void StepsWhereTwoKeysShareATime()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 0).Key(1, 0).Key(1, 5).Key(2, 5).Build();

        curve.Evaluate(0.999f).Should().BeApproximately(0, Precision);
        curve.Evaluate(1).Should().BeApproximately(5, Precision);
        curve.Evaluate(1.5f).Should().BeApproximately(5, Precision);
    }

    [Fact]
    public void ReadsItsKeysFromJson()
    {
        const string json = """
            {
              "keys": [
                { "time": 0, "value": 1, "mode": "Linear" },
                { "time": 2, "value": 3, "mode": "Free", "in": -1, "out": 2 },
                { "time": 4, "value": 0 },
              ],
            }
            """;

        Curve? curve = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.Curve);

        curve.Should().NotBeNull();
        CurveKey free = new() { Time = 2, Value = 3, Mode = CurveMode.Free, In = -1, Out = 2 };
        curve.Keys.Should().HaveCount(3);
        curve.Keys[1].Should().Be(free);
        curve.Keys[2].Mode.Should().Be(CurveMode.Smooth);
        curve.Evaluate(1).Should().BeApproximately(2, Precision);
    }

    [Fact]
    public void NamesItsModesInItsSchema()
    {
        JsonObject schema = JsonSchemas.Create(RuntimeJsonContext.Default.Curve);

        JsonNode key = schema["properties"]!["keys"]!["items"]!;
        JsonArray modes = key["properties"]!["mode"]!["enum"]!.AsArray();
        modes.Select(mode => mode!.GetValue<string>()).Should().Equal("Smooth", "Linear", "Constant", "Free");
    }

    [Fact]
    public void IsEmptyWhenAFileGivesNoKeys()
    {
        Curve? curve = JsonSerializer.Deserialize("{}", RuntimeJsonContext.Default.Curve);

        curve.Should().NotBeNull();
        curve.Keys.Should().BeEmpty();
    }

    [Fact]
    public void WritesModesByNameAndLeavesDefaultsOut()
    {
        Curve curve = new CurveTestsBuilder().Key(0, 1).Key(2, 3, CurveMode.Linear).FreeKey(4, 0, -1, 2).Build();

        string json = JsonSerializer.Serialize(curve, RuntimeJsonContext.Default.Curve);

        json.Should().Be("""{"keys":[{"time":0,"value":1},{"time":2,"value":3,"mode":"Linear"},{"time":4,"value":0,"mode":"Free","in":-1,"out":2}]}""");
    }
}
