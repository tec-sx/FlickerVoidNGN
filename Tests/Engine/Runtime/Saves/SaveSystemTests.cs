using System.Numerics;
using System.Text.Json.Nodes;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Saves;

public sealed class SaveSystemTests : IDisposable
{
    private static readonly Tag Quest = new("Quest.Cellar");
    private static readonly Tag Fame = new("Fact.Social.Fame");
    private static readonly Tag Dialogue = new("Save.Block.Dialogue");

    private const string Crates = """
        { "name": "Crate", "components": { "transform": { "position": [1, 0, 0] }, "saveable": { "id": "Crate" } } },
        { "name": "Barrel", "components": { "transform": { "position": [2, 0, 0] }, "saveable": { "id": "Barrel" } } },
        { "name": "Dust", "components": { "transform": { "position": [3, 0, 0] } } }
        """;

    private readonly SaveSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ASaveBringsBackTheFactsAndTheGamesState()
    {
        string weather = "Rain";
        _builder.Build();
        _builder.Game.Data.AddResolver(RuntimeJsonContext.Default);
        _builder.Game.SaveStates.AddGlobal(
            "weather",
            () => new JsonObject { ["now"] = weather },
            saved => weather = saved["now"]!.GetValue<string>());
        _builder.Game.Facts.SetFact(Quest, 1);
        _builder.Game.Facts.SetFact(Fame, 40);

        bool saved = _builder.Saves.Save("Slot1");
        _builder.Game.Facts.SetFact(Fame, 90);
        _builder.Game.Facts.SetFact(new Tag("Fact.Later"), 1);
        weather = "Sun";
        bool loaded = _builder.Saves.Load("Slot1");

        saved.Should().BeTrue();
        loaded.Should().BeTrue();
        _builder.Game.Facts.AllFacts.Should().BeEquivalentTo(new Dictionary<Tag, int> { [Quest] = 1, [Fame] = 40 });
        weather.Should().Be("Rain");
    }

    [Fact]
    public void ALoadRollsWhatTheGamesRandomWouldHaveRolledAfterTheSave()
    {
        _builder.Build();
        _builder.Saves.Save("Slot1");
        int[] rolled = GameRandomTestsBuilder.Roll(_builder.Game.Random, 4);

        _builder.Saves.Load("Slot1");

        GameRandomTestsBuilder.Roll(_builder.Game.Random, 4).Should().Equal(rolled);
    }

    [Fact]
    public void SaveableEntitiesComeBackAsTheyWereSaved()
    {
        _builder.Build();
        IReadOnlyList<Entity> entities = _builder.Spawn(Crates);
        _builder.Game.World.Get<Transform>(entities[0]).Position = new Vector3(5, 0, 0);
        _builder.Saves.Save("Slot1");
        _builder.Game.World.Get<Transform>(entities[0]).Position = new Vector3(9, 0, 0);
        _builder.Game.World.Get<Transform>(entities[2]).Position = new Vector3(9, 0, 0);

        _builder.Saves.Load("Slot1");

        _builder.Game.World.Get<Transform>(entities[0]).Position.Should().Be(new Vector3(5, 0, 0));
        _builder.Game.World.Get<Transform>(entities[2]).Position.Should().Be(new Vector3(9, 0, 0), "it isn't saveable");
    }

    [Fact]
    public void SaveableEntitiesDestroyedBeforeTheSaveAreDestroyedWhenItLoads()
    {
        _builder.Build();
        IReadOnlyList<Entity> entities = _builder.Spawn(Crates);
        _builder.Game.Lifecycle.Destroy(_builder.Game.World, entities[1]);
        _builder.Saves.Save("Slot1");

        _builder.StartAgain();
        IReadOnlyList<Entity> again = _builder.Spawn(Crates);
        _builder.Saves.Load("Slot1");

        _builder.Game.World.IsAlive(again[0]).Should().BeTrue();
        _builder.Game.World.IsAlive(again[1]).Should().BeFalse();
        _builder.Game.World.IsAlive(again[2]).Should().BeTrue();
    }

    [Fact]
    public void ModulesSaveTheirOwnStateOfEachSaveableEntity()
    {
        Dictionary<Entity, string> labels = [];
        _builder.Build();
        _builder.Game.Data.AddResolver(RuntimeJsonContext.Default);
        _builder.Game.SaveStates.Add(
            "label",
            entity => labels.TryGetValue(entity, out string? label) ? new JsonObject { ["text"] = label } : null,
            (entity, saved) => labels[entity] = saved["text"]!.GetValue<string>());
        IReadOnlyList<Entity> entities = _builder.Spawn(Crates);
        labels[entities[0]] = "Wine";
        _builder.Saves.Save("Slot1");
        labels.Clear();

        _builder.Saves.Load("Slot1");

        labels.Should().Equal(new Dictionary<Entity, string> { [entities[0]] = "Wine" });
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
