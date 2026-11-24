using System.Numerics;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Flow.Scenes;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Flow.Tests.Scenes;

public sealed class TravelTests : IDisposable
{
    private static readonly Tag Arrived = new("Fact.Cellar.Arrived");

    private readonly TravelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheTravelNodeTravelsBeforeTheNextTickAndGoesOnOnceThere()
    {
        FlowRun run = _builder.Run("""
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "travel", "id": "down", "scene": "Scenes/Cellar.fvscene", "entry": "Stairs" },
                { "$type": "applyEffects", "id": "there", "effects": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Arrived" } ] } }
              ],
              "links": [ { "from": "start", "to": "down" }, { "from": "down", "to": "there" } ]
            }
            """);
        string? before = _builder.Travel.Current;
        int arrivedBefore = _builder.Game.Facts.GetFact(Arrived);

        _builder.Tick();

        before.Should().Be(TravelTestsBuilder.Street, "travel waits for the tick, as it replaces entities systems use");
        arrivedBefore.Should().Be(0);
        _builder.Travel.Current.Should().Be(TravelTestsBuilder.Cellar);
        _builder.FoxPosition().Should().Be(new Vector3(9, -3, 0));
        _builder.Game.Facts.GetFact(Arrived).Should().Be(1);
        run.ActiveNodes.Should().BeEmpty();
    }

    [Fact]
    public void TheTravelEffectTravelsBeforeTheNextTick()
    {
        EffectList effects = new() { Effects = [new TravelEffect { Scene = TravelTestsBuilder.Cellar, Entry = "Stairs" }] };

        effects.Apply(_builder.Context);
        _builder.Tick();

        _builder.Travel.Current.Should().Be(TravelTestsBuilder.Cellar);
        _builder.FoxPosition().Should().Be(new Vector3(9, -3, 0));
    }
}
