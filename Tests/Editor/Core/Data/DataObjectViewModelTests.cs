using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Editor.Core.Tests.Data;

public sealed class DataObjectViewModelTests
{
    private readonly DataObjectViewModelTestsBuilder _builder = new();

    [Fact]
    public void ShowsValuesAsFieldsNestedObjectsAsSectionsAndFragmentsAsAList()
    {
        DataObjectViewModel form = _builder.Build();

        form.Fields.Select(static field => field.Label).Should().Contain(["Id", "Slots"]);
        form.Objects.Should().ContainSingle().Which.Title.Should().Be("Display");
        DataListViewModel fragments = form.Lists.Should().ContainSingle().Subject;
        fragments.Choices.Should().Equal("locked", "loot");
        fragments.Items.Should().ContainSingle().Which.Form.Title.Should().Be("locked");
    }

    [Fact]
    public void EditsTheObjectInPlace()
    {
        DataObjectViewModel form = _builder.Build();
        TextFieldViewModel id = (TextFieldViewModel)form.Fields.Single(static field => field.Label == "Id");

        id.Value = "Crate.Cellar";

        _builder.Crate.Id.Should().Be(new Tag("Crate.Cellar"));
        _builder.Edits.Should().Be(1);
    }

    [Fact]
    public void EditsAFragmentInPlace()
    {
        DataObjectViewModel form = _builder.Build();
        DataObjectViewModel locked = form.Lists[0].Items[0].Form;
        TextFieldViewModel key = (TextFieldViewModel)locked.Fields.Single();

        key.Value = "Items/Attic.fvdata";

        _builder.Crate.Fragments[0].Should().BeOfType<LockedFragment>().Which.Key.Should().Be("Items/Attic.fvdata");
    }

    [Fact]
    public void AddsAFragmentOfTheChosenType()
    {
        DataObjectViewModel form = _builder.Build();
        DataListViewModel fragments = form.Lists[0];

        fragments.Add("loot");

        _builder.Crate.Fragments.Should().HaveCount(2);
        _builder.Crate.Fragments[1].Should().BeOfType<LootFragment>().Which.Count.Should().Be(1);
        fragments.Items.Should().HaveCount(2);
        _builder.Edits.Should().Be(1);
    }

    [Fact]
    public void RemovesAFragment()
    {
        DataObjectViewModel form = _builder.Build();
        DataListViewModel fragments = form.Lists[0];

        fragments.Remove(fragments.Items[0]);

        _builder.Crate.Fragments.Should().BeEmpty();
        fragments.Items.Should().BeEmpty();
        _builder.Edits.Should().Be(1);
    }
}
