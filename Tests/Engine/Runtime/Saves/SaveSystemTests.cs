using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Saves;

namespace FlickerVoid.Engine.Runtime.Tests.Saves;

public sealed class SaveSystemTests : IDisposable
{
    private static readonly Tag Quest = new("Quest.Cellar");
    private static readonly Tag Fame = new("Fact.Social.Fame");
    private static readonly Tag Dialogue = new("Save.Block.Dialogue");

    private readonly SaveSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ASaveBringsBackTheFactsAndWhatModulesWrote()
    {
        _builder.Build();
        _builder.Game.Facts.SetFact(Quest, 1);
        _builder.Game.Facts.SetFact(Fame, 40);
        _builder.Saves.Writing += save => save.CustomData["Door"] = "Open";

        bool saved = _builder.Saves.Save("Slot1");
        _builder.Game.Facts.SetFact(Fame, 90);
        _builder.Game.Facts.SetFact(new Tag("Fact.Later"), 1);
        string? door = null;
        _builder.Saves.Loaded += save => door = save.CustomData["Door"];
        bool loaded = _builder.Saves.Load("Slot1");

        saved.Should().BeTrue();
        loaded.Should().BeTrue();
        _builder.Game.Facts.AllFacts.Should().BeEquivalentTo(new Dictionary<Tag, int> { [Quest] = 1, [Fame] = 40 });
        door.Should().Be("Open");
    }

    [Fact]
    public void LoadedRunsAfterTheFactsAreBack()
    {
        _builder.Build();
        _builder.Game.Facts.SetFact(Quest, 2);
        _builder.Saves.Save("Slot1");
        _builder.Game.Facts.SetFact(Quest, 0);
        int questOnLoad = -1;
        _builder.Saves.Loaded += _ => questOnLoad = _builder.Game.Facts.GetFact(Quest);

        _builder.Saves.Load("Slot1");

        questOnLoad.Should().Be(2);
    }

    [Fact]
    public void SlotsListTheirMetadataNewestFirst()
    {
        _builder.Build();
        _builder.Play(1);
        _builder.Saves.Save("First");
        _builder.Play(1);
        _builder.Saves.SaveCheckpoint();

        IReadOnlyList<SaveMetadata> slots = _builder.Saves.GetSlots();

        slots.Select(slot => slot.Slot).Should().Equal("Checkpoint", "First");
        slots[0].IsCheckpoint.Should().BeTrue();
        slots[0].Version.Should().Be(SaveGame.CurrentVersion);
        slots[0].PlayTime.Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100));
        slots[1].PlayTime.Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void PlayTimeGoesOnFromTheLoadedSave()
    {
        _builder.Build();
        _builder.Play(2);
        _builder.Saves.Save("Slot1");
        _builder.Play(3);
        _builder.Saves.Load("Slot1");
        _builder.Play(1);

        _builder.Saves.PlayTime.Should().BeCloseTo(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void ABlockedGameCannotSave()
    {
        _builder.Build();
        _builder.Saves.AddBlock(Dialogue);

        bool blocked = _builder.Saves.Save("Slot1");
        _builder.Saves.RemoveBlock(Dialogue);
        bool saved = _builder.Saves.Save("Slot1");

        blocked.Should().BeFalse();
        saved.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("../Escape")]
    [InlineData("a/b")]
    public void ASlotNameMustBeAPlainFileName(string slot)
    {
        _builder.Build();

        bool saved = _builder.Saves.Save(slot);

        saved.Should().BeFalse();
    }

    [Fact]
    public void MissingNewerAndBrokenSavesDoNotLoad()
    {
        _builder.Build();
        _builder.Game.Facts.SetFact(Fame, 5);
        Directory.CreateDirectory(_builder.Folder);
        File.WriteAllText(Path.Combine(_builder.Folder, "Broken" + SaveSystem.Extension), "{ not json");
        string newer = $$"""{ "metadata": { "version": {{SaveGame.CurrentVersion + 1}}, "slot": "Newer" }, "facts": {} }""";
        File.WriteAllText(Path.Combine(_builder.Folder, "Newer" + SaveSystem.Extension), newer);

        bool missing = _builder.Saves.Load("Missing");
        bool broken = _builder.Saves.Load("Broken");
        bool tooNew = _builder.Saves.Load("Newer");

        missing.Should().BeFalse();
        broken.Should().BeFalse();
        tooNew.Should().BeFalse();
        _builder.Game.Facts.GetFact(Fame).Should().Be(5);
        _builder.Saves.GetSlots().Select(slot => slot.Slot).Should().Equal("Newer");
    }

    [Fact]
    public void ADeletedSlotIsGone()
    {
        _builder.Build();
        _builder.Saves.Save("Slot1");

        bool deleted = _builder.Saves.Delete("Slot1");
        bool again = _builder.Saves.Delete("Slot1");

        deleted.Should().BeTrue();
        again.Should().BeFalse();
        _builder.Saves.GetSlots().Should().BeEmpty();
    }
}
