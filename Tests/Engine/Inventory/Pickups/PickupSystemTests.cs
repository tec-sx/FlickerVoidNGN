using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Pickups;

namespace FlickerVoid.Engine.Inventory.Tests.Pickups;

public sealed class PickupSystemTests : IDisposable
{
    private readonly PickupSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void PickingUpEverythingDestroysThePickup()
    {
        _builder.Build("\"item\": \"Items/Apple.fvdata\", \"quantity\": 3");
        List<PickedUpEvent> picked = _builder.Collect<PickedUpEvent>();

        bool done = _builder.Pickups.TryPickUp(_builder.Hero, _builder.Pickup);
        _builder.Game.Events.Dispatch();

        done.Should().BeTrue();
        picked.Should().Equal(new PickedUpEvent(_builder.Pickup, _builder.Hero, 3));
        _builder.Inventories.GetStacks(_builder.Hero).Should().ContainSingle().Which.Quantity.Should().Be(3);
        _builder.Game.World.IsAlive(_builder.Pickup).Should().BeFalse();
    }

    [Fact]
    public void APartialPickupLeavesTheRest()
    {
        _builder.WithWeightLimit(2).Build("\"item\": \"Items/Apple.fvdata\", \"quantity\": 5");

        _builder.Pickups.TryPickUp(_builder.Hero, _builder.Pickup);
        _builder.Game.Events.Dispatch();
        bool again = _builder.Pickups.TryPickUp(_builder.Hero, _builder.Pickup);

        again.Should().BeFalse("nothing more fits");
        _builder.Left().Should().Be(3);
    }

    [Fact]
    public void AKeptPickupStaysEmpty()
    {
        _builder.Build("\"item\": \"Items/Apple.fvdata\", \"destroyWhenEmpty\": false");

        _builder.Pickups.TryPickUp(_builder.Hero, _builder.Pickup);
        _builder.Game.Events.Dispatch();

        _builder.Game.World.IsAlive(_builder.Pickup).Should().BeTrue();
        _builder.Left().Should().Be(0);
        _builder.Pickups.CanPickUp(_builder.Hero, _builder.Pickup).Should().BeFalse();
    }

    [Fact]
    public void UsingInPlaceSpendsAChargeAndTheLastOneEmptiesIt()
    {
        _builder.Build("\"item\": \"Items/Bottle.fvdata\"");

        bool first = _builder.Pickups.UseInPlace(_builder.Hero, _builder.Pickup);
        int left = _builder.Left();
        bool second = _builder.Pickups.UseInPlace(_builder.Hero, _builder.Pickup);
        _builder.Game.Events.Dispatch();

        first.Should().BeTrue();
        left.Should().Be(1, "one charge of two was spent");
        second.Should().BeTrue();
        _builder.Game.Facts.GetFact(new Tag("Fact.Sips")).Should().Be(2);
        _builder.Game.World.IsAlive(_builder.Pickup).Should().BeFalse();
    }

    [Fact]
    public void APickedUpPartlyUsedBottleKeepsItsChargesLeft()
    {
        _builder.Build("\"item\": \"Items/Bottle.fvdata\", \"quantity\": 2");

        _builder.Pickups.UseInPlace(_builder.Hero, _builder.Pickup);
        _builder.Pickups.TryPickUp(_builder.Hero, _builder.Pickup);

        IReadOnlyList<ItemStack> stacks = _builder.Inventories.GetStacks(_builder.Hero);
        stacks.Select(static stack => stack.UsesLeft).Should().Equal(1, 2);
    }

    [Fact]
    public void ThePickUpEffectTakesTheTargetIntoTheInstigatorsInventory()
    {
        _builder.Build("\"item\": \"Items/Apple.fvdata\", \"quantity\": 2");

        new PickUpEffect().Apply(_builder.Context());

        _builder.Inventories.GetStacks(_builder.Hero).Should().ContainSingle().Which.Quantity.Should().Be(2);
    }
}
