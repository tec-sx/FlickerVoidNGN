using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Resources.Knowledge.Scanning;

public sealed class ScannerSystemTests : IDisposable
{
    private const string Plain = """{ "type": "scan", "display": { "name": "Crate" }, "scanDuration": 1 }""";

    private const string Counted = """
        { "type": "scan", "scanDuration": 1,
          "onScanned": { "effects": [ { "$type": "fact", "fact": "Scan.Crate", "write": "Add" } ] } }
        """;

    private const string Hidden = """
        { "type": "scan", "scannableWhen": { "conditions": [ { "$type": "fact", "fact": "Knowledge.Crates" } ] } }
        """;

    private const string Entries = """
        { "type": "scan", "entries": [
          { "label": "Wood", "text": "Old oak." },
          { "label": "Marks", "text": "Scratched by a knife.", "visibleWhen": { "conditions": [ { "$type": "fact", "fact": "Knowledge.Knives" } ] } },
          { "label": "Smell", "text": "Wine." } ] }
        """;

    private readonly ScannerSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void RevealsTheScannablesInRangeWhileScanModeIsOn()
    {
        _builder.WithFile("Crate.fvdata", Plain).Build();
        Entity near = _builder.AddScannable(new Vector3(3, 0, 0), "Crate.fvdata");
        Entity far = _builder.AddScannable(new Vector3(12, 0, 0), "Crate.fvdata");

        _builder.Press(ScannerSystem.ScanMode);
        _builder.Run(0.2f);
        bool nearShown = _builder.Scanner.IsRevealed(near);
        bool farShown = _builder.Scanner.IsRevealed(far);
        _builder.Press(ScannerSystem.ScanMode);
        _builder.Run(0.2f);

        nearShown.Should().BeTrue();
        farShown.Should().BeFalse();
        _builder.Scanner.IsActive(_builder.Hero).Should().BeFalse();
        _builder.Scanner.IsRevealed(near).Should().BeFalse();
    }

    [Fact]
    public void LeavesOutScannablesWhoseConditionsFail()
    {
        _builder.WithFile("Hidden.fvdata", Hidden).Build();
        Entity crate = _builder.AddScannable(new Vector3(3, 0, 0), "Hidden.fvdata");

        _builder.Press(ScannerSystem.ScanMode);
        _builder.Run(0.2f);
        bool before = _builder.Scanner.IsRevealed(crate);
        _builder.Game.Facts.SetFact(new Tag("Knowledge.Crates"), 1);
        _builder.Run(0.2f);

        before.Should().BeFalse();
        _builder.Scanner.IsRevealed(crate).Should().BeTrue();
    }

    [Fact]
    public void FocusesTheRevealedScannableThePointerIsOn()
    {
        _builder.WithFile("Crate.fvdata", Plain).Build();
        Entity crate = _builder.AddScannable(new Vector3(3, 0, 0), "Crate.fvdata");
        _builder.PointAt(new Vector3(3, 0, 0));

        _builder.Run(0.2f);
        Entity unscanning = _builder.Scanner.GetFocus(_builder.Hero);
        _builder.Press(ScannerSystem.ScanMode);
        _builder.Run(0.2f);
        Entity focused = _builder.Scanner.GetFocus(_builder.Hero);
        _builder.PointAt(new Vector3(-3, 0, 0));
        _builder.Run(0.2f);

        unscanning.Should().Be(Entity.Null);
        focused.Should().Be(crate);
        _builder.Scanner.GetFocus(_builder.Hero).Should().Be(Entity.Null);
    }

    [Fact]
    public void NeedsALineOfSightToFocus()
    {
        _builder.WithFile("Crate.fvdata", Plain).Build();
        Entity crate = _builder.AddScannable(new Vector3(0, 0, 4), "Crate.fvdata");
        _builder.AddWall(2);
        _builder.PointAt(new Vector3(0, 0, 4));

        _builder.Press(ScannerSystem.ScanMode);
        _builder.Run(0.2f);

        _builder.Scanner.IsRevealed(crate).Should().BeTrue();
        _builder.Scanner.GetFocus(_builder.Hero).Should().Be(Entity.Null);
    }

    [Fact]
    public void ScansTheFocusWhileScanIsHeldAndAppliesItsEffectsOnce()
    {
        _builder.WithFile("Crate.fvdata", Counted).Build();
        Entity crate = _builder.AddScannable(new Vector3(3, 0, 0), "Crate.fvdata");
        _builder.PointAt(new Vector3(3, 0, 0));
        _builder.Press(ScannerSystem.ScanMode);
        _builder.Run(0.2f);
        List<ScannedEvent> scanned = [];
        _builder.Game.Events.Handle((in ScannedEvent e) => scanned.Add(e));

        _builder.Press(ScannerSystem.Scan);
        _builder.Run(0.5f);
        float halfway = _builder.Scanner.GetProgress(_builder.Hero);
        bool scannedHalfway = _builder.Scanner.WasScanned(crate);
        _builder.Run(0.6f);
        _builder.Release(ScannerSystem.Scan);
        _builder.Press(ScannerSystem.Scan);
        _builder.Run(1.5f);

        halfway.Should().BeInRange(0.4f, 0.6f);
        scannedHalfway.Should().BeFalse();
        _builder.Scanner.WasScanned(crate).Should().BeTrue();
        _builder.Game.Facts.GetFact(new Tag("Scan.Crate")).Should().Be(1);
        scanned.Should().ContainSingle().Which.Should().Be(new ScannedEvent(_builder.Hero, crate));
        _builder.Scanner.GetProgress(_builder.Hero).Should().Be(0);
    }

    [Fact]
    public void LettingGoStartsTheScanOver()
    {
        _builder.WithFile("Crate.fvdata", Plain).Build();
        Entity crate = _builder.AddScannable(new Vector3(3, 0, 0), "Crate.fvdata");
        _builder.PointAt(new Vector3(3, 0, 0));
        _builder.Press(ScannerSystem.ScanMode);
        _builder.Run(0.2f);

        _builder.Press(ScannerSystem.Scan);
        _builder.Run(0.6f);
        _builder.Release(ScannerSystem.Scan);
        float released = _builder.Scanner.GetProgress(_builder.Hero);
        _builder.Press(ScannerSystem.Scan);
        _builder.Run(0.6f);

        released.Should().Be(0);
        _builder.Scanner.WasScanned(crate).Should().BeFalse();
    }

    [Fact]
    public void ListsTheEntriesWhoseConditionsPass()
    {
        _builder.WithFile("Crate.fvdata", Entries).Build();
        Entity crate = _builder.AddScannable(new Vector3(3, 0, 0), "Crate.fvdata");

        IReadOnlyList<ScanEntry> before = _builder.Scanner.GetVisibleEntries(_builder.Hero, crate);
        _builder.Game.Facts.SetFact(new Tag("Knowledge.Knives"), 1);
        IReadOnlyList<ScanEntry> after = _builder.Scanner.GetVisibleEntries(_builder.Hero, crate);

        before.Select(entry => entry.Label).Should().Equal("Wood", "Smell");
        after.Select(entry => entry.Label).Should().Equal("Wood", "Marks", "Smell");
    }
}
