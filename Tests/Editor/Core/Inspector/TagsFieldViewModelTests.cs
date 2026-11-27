using System.Text.Json;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Tags;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Editor.Core.Tests.Inspector;

public sealed class TagsFieldViewModelTests
{
    private readonly TagCatalog _catalog = new();

    [Fact]
    public void ShowsOneTagAsAChipAndPicksOnlyOne()
    {
        Holder holder = new(typeof(Tag), new Tag("Knowledge.Cellar"));
        TagsFieldViewModel field = (TagsFieldViewModel)FieldFactory.Create(holder);
        _catalog.Add("Knowledge.Inn");

        field.IsSingle.Should().BeTrue();
        field.Tags.Should().Equal("Knowledge.Cellar");

        TagPickerViewModel picker = field.OpenPicker(_catalog);
        picker.ToggleCommand.Execute(picker.Rows.First(row => row.Path == "Knowledge.Inn"));

        holder.Value.Should().Be(new Tag("Knowledge.Inn"));
        field.CanAdd.Should().BeFalse("a value holding one tag has no room for another");

        field.RemoveCommand.Execute("Knowledge.Inn");

        holder.Value.Should().Be(Tag.None);
        field.CanAdd.Should().BeTrue();
    }

    [Fact]
    public void WritesAnArrayOfTagsAsAnArray()
    {
        Holder holder = new(typeof(Tag[]), new[] { new Tag("Item.Key") });
        TagsFieldViewModel field = (TagsFieldViewModel)FieldFactory.Create(holder);

        field.RemoveCommand.Execute("Item.Key");

        holder.Value.Should().BeOfType<Tag[]>().Which.Should().BeEmpty();
    }

    private sealed class Holder(Type type, object value) : IEditableValue
    {
        public object? Value { get; private set; } = value;

        public string Label => "Tags";

        public Type Type => type;

        public bool CanWrite => true;

        public bool AcceptsNull => false;

        public JsonSerializerOptions? Options => null;

        public TAttribute? GetAttribute<TAttribute>()
            where TAttribute : Attribute
        {
            return null;
        }

        public object? Get() => Value;

        public void Set(object? value) => Value = value;

        public string ToJson(object? value) => "";
    }
}
