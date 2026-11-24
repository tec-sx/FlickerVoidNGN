using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

public sealed class SceneTravelTests : IDisposable
{
    private const string Street = SceneTravelTestsBuilder.Street;
    private const string Cellar = SceneTravelTestsBuilder.Cellar;

    private readonly SceneTravelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TravellingReplacesTheSceneLoadedWithTheNextOneStarted()
    {
        SceneTravel travel = _builder.Build().Travel;
        travel.Travel(Street);

        travel.Travel(Cellar);

        travel.Current.Should().Be(Cellar);
        _builder.Find("Crate").Should().Be(Entity.Null);
        Entity barrel = _builder.Find("Barrel");
        _builder.Game.World.Has<Active>(barrel).Should().BeTrue();
    }

    [Fact]
    public void TravellersStandAtTheEntryNamed()
    {
        SceneTravel travel = _builder.Build().Travel;
        travel.Travel(Street);

        travel.Travel(Cellar, entry: "Stairs");

        _builder.TransformOf("Fox").Position.Should().Be(new Vector3(9, -3, 0));
        _builder.TransformOf("Fox").Rotation.Should().Be(new Quaternion(0, 1, 0, 0));
    }

    [Fact]
    public void ARequestedTravelWaitsForTheNextTick()
    {
        SceneTravel travel = _builder.Build().Travel;
        travel.Travel(Street);

        travel.Request(Cellar);
        string? before = travel.Current;
        _builder.Tick();

        before.Should().Be(Street);
        travel.Current.Should().Be(Cellar);
    }

    [Fact]
    public void AScenesEntitiesAreAsTheyWereLeftWhenTheGameComesBack()
    {
        SceneTravel travel = _builder.Build().Travel;
        travel.Travel(Street);
        _builder.TransformOf("Crate").Position = new Vector3(5, 0, 0);
        _builder.Destroy("Key");

        travel.Travel(Cellar);
        travel.Travel(Street);

        _builder.TransformOf("Crate").Position.Should().Be(new Vector3(5, 0, 0));
        _builder.Find("Key").Should().Be(Entity.Null);
    }

    [Fact]
    public void ALoadInTheSameSessionBringsBackWhatWasDestroyedAfterTheSave()
    {
        _builder.Build();
        _builder.Travel.Travel(Street);
        _builder.Saves.Save("Slot1");
        _builder.Destroy("Key");
        _builder.TransformOf("Crate").Position = new Vector3(5, 0, 0);

        bool loaded = _builder.Saves.Load("Slot1");

        loaded.Should().BeTrue();
        _builder.Find("Key").Should().NotBe(Entity.Null);
        _builder.TransformOf("Crate").Position.Should().Be(new Vector3(1, 0, 0));
    }

    [Fact]
    public void ALoadTravelsBackToTheSceneTheSaveWasMadeIn()
    {
        _builder.Build();
        _builder.Travel.Travel(Street);
        _builder.Saves.Save("Slot1");
        _builder.Travel.Travel(Cellar);

        _builder.Saves.Load("Slot1");

        _builder.Travel.Current.Should().Be(Street);
        _builder.Find("Crate").Should().NotBe(Entity.Null);
        _builder.Find("Barrel").Should().Be(Entity.Null);
    }

    [Fact]
    public void ASaveKeepsTheScenesLeftAsTheyWereLeft()
    {
        _builder.Build();
        _builder.Travel.Travel(Street);
        _builder.TransformOf("Crate").Position = new Vector3(5, 0, 0);
        _builder.Travel.Travel(Cellar);
        _builder.Saves.Save("Slot1");
        _builder.Travel.Travel(Street);
        _builder.TransformOf("Crate").Position = new Vector3(8, 0, 0);
        _builder.Travel.Travel(Cellar);

        _builder.Saves.Load("Slot1");
        _builder.Travel.Travel(Street);

        _builder.TransformOf("Crate").Position.Should().Be(new Vector3(5, 0, 0));
    }

    [Fact]
    public void TravellersTakeWhatTheyCarryToTheNextScene()
    {
        _builder.Build();
        _builder.Game.SaveStates.AddComponent("purse", SceneTravelTestsJsonContext.Default.Purse);
        _builder.Game.Spawner.Register("purse", SceneTravelTestsJsonContext.Default.Purse);
        _builder.Travel.Travel(Street);
        Entity fox = _builder.Find("Fox");
        _builder.Game.World.Add(fox, new Purse { Coins = 12 });

        _builder.Travel.Travel(Cellar, entry: "Stairs");

        Entity arrived = _builder.Find("Fox");
        _builder.Game.World.Get<Purse>(arrived).Coins.Should().Be(12);
        _builder.TransformOf("Fox").Position.Should().Be(new Vector3(9, -3, 0), "the entry places it, not where it stood");
    }
}
