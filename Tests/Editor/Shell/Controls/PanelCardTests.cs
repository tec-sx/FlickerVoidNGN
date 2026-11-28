using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Persistence.Layout;

namespace FlickerVoid.Editor.Tests.Controls;

public sealed class PanelCardTests
{
    private readonly PanelCardTestsBuilder _builder = new();

    [Fact]
    public async Task ShowsItsRowsWhileOpenAndHidesThemWhenFolded()
    {
        await _builder.Run(() =>
        {
            _builder.Card.IsExpanded.Should().BeTrue("a card starts open");
            _builder.Short.IsEffectivelyVisible.Should().BeTrue();

            _builder.Card.IsExpanded = false;
            PanelCardTestsBuilder.Settle();

            _builder.Short.IsEffectivelyVisible.Should().BeFalse();
        });
    }

    [Fact]
    public async Task FoldsAndOpensWhenItsHeaderIsClicked()
    {
        await _builder.Run(() =>
        {
            ToggleButton header = _builder.Part<ToggleButton>("PART_Header");

            _builder.Click(header);
            _builder.Card.IsExpanded.Should().BeFalse();

            _builder.Click(header);
            _builder.Card.IsExpanded.Should().BeTrue();
        });
    }

    [Fact]
    public async Task HasTheInspectorsHeader()
    {
        await _builder.Run(() =>
        {
            ToggleButton header = _builder.Part<ToggleButton>("PART_Header");
            IBrush raised = (IBrush)_builder.Card.FindResource("EditorControlBrush")!;

            header.Bounds.Height.Should().Be(24);
            header.Bounds.Width.Should().Be(300, "the header spans the panel");
            header.Background.Should().Be(raised, "an open card's header is raised");
            header.FontWeight.Should().Be(FontWeight.SemiBold);

            _builder.Card.IsExpanded = false;
            PanelCardTestsBuilder.Settle();

            ISolidColorBrush sunk = (ISolidColorBrush)header.Background!;
            sunk.Color.Should().Be(Colors.Transparent, "a folded card's header sinks into the panel");
            header.FontWeight.Should().Be(FontWeight.Medium);
        });
    }

    [Fact]
    public async Task TurnsItsChevronWithTheFold()
    {
        await _builder.Run(() =>
        {
            PathIcon opened = _builder.Part<PathIcon>("PART_Opened");
            PathIcon closed = _builder.Part<PathIcon>("PART_Closed");

            opened.IsVisible.Should().BeTrue();
            closed.IsVisible.Should().BeFalse();

            _builder.Card.IsExpanded = false;
            PanelCardTestsBuilder.Settle();

            opened.IsVisible.Should().BeFalse();
            closed.IsVisible.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ShowsItsIconOnlyWhenItHasOne()
    {
        await _builder.Run(() =>
        {
            PathIcon icon = _builder.Part<PathIcon>("PART_Icon");
            icon.IsVisible.Should().BeFalse();

            _builder.Card.Icon = LucideIcons.Cube;
            PanelCardTestsBuilder.Settle();

            icon.IsVisible.Should().BeTrue();
            icon.Data.Should().BeSameAs(LucideIcons.Cube);
        });
    }

    [Fact]
    public async Task SaysWhatItHoldsOnlyWhileFolded()
    {
        await _builder.Run(() =>
        {
            TextBlock summary = _builder.Part<TextBlock>("PART_Summary");
            _builder.Card.Summary = "2 × 2 × 2 m";
            PanelCardTestsBuilder.Settle();
            summary.IsVisible.Should().BeFalse("an open card shows its rows instead");

            _builder.Card.IsExpanded = false;
            PanelCardTestsBuilder.Settle();

            summary.IsVisible.Should().BeTrue();
            summary.Text.Should().Be("2 × 2 × 2 m");
        });
    }

    [Fact]
    public async Task ShowsItsNoteBesideTheTitleOpenOrFolded()
    {
        await _builder.Run(() =>
        {
            TextBlock note = _builder.Part<TextBlock>("PART_Note");
            note.IsVisible.Should().BeFalse("a card without a note keeps no room for one");

            _builder.Card.Note = "2";
            PanelCardTestsBuilder.Settle();
            note.IsVisible.Should().BeTrue();

            _builder.Card.IsExpanded = false;
            PanelCardTestsBuilder.Settle();
            note.IsVisible.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ShowsTheMoreButtonOnlyWithAMenuToOpen()
    {
        await _builder.Run(() =>
        {
            Button more = _builder.Part<Button>("PART_More");
            more.IsVisible.Should().BeFalse("a card without a menu has no More button");

            _builder.Card.Menu = new MenuFlyout { Items = { new MenuItem { Header = "Remove" } } };
            PanelCardTestsBuilder.Settle();
            more.IsVisible.Should().BeTrue();
            more.Bounds.Width.Should().Be(18);
            more.Bounds.Height.Should().Be(18);

            _builder.Card.ShowsMenu = false;
            PanelCardTestsBuilder.Settle();
            more.IsVisible.Should().BeFalse();
        });
    }

    [Fact]
    public async Task OpensItsMenuFromTheMoreButtonWithoutFolding()
    {
        await _builder.Run(() =>
        {
            MenuFlyout menu = new() { Items = { new MenuItem { Header = "Remove" } } };
            _builder.Card.Menu = menu;
            PanelCardTestsBuilder.Settle();

            Button more = _builder.Part<Button>("PART_More");
            _builder.Click(more);

            menu.IsOpen.Should().BeTrue();
            _builder.Card.IsExpanded.Should().BeTrue();
            menu.Hide();
        });
    }

    [Fact]
    public async Task KeepsItsActionsAtTheHeadersEndAndClickable()
    {
        await _builder.Run(() =>
        {
            int clicks = 0;
            Button add = new() { Classes = { "icon", "small" }, Content = new PathIcon { Data = LucideIcons.Add } };
            add.Click += (_, _) => clicks++;
            _builder.Card.Actions = add;
            PanelCardTestsBuilder.Settle();

            add.IsEffectivelyVisible.Should().BeTrue();
            double right = _builder.LeftOf(add) + add.Bounds.Width;
            right.Should().BeApproximately(_builder.LeftOf(_builder.Card) + 300 - 6, 0.5, "it sits at the header's end, inside its padding");

            _builder.Click(add);

            clicks.Should().Be(1);
            _builder.Card.IsExpanded.Should().BeTrue("a click on an action is not a click on the header");
            add.DataContext.Should().BeSameAs(_builder.Card.DataContext);
            add.Parent.Should().BeSameAs(_builder.Card, "bindings and styles reach it through the card");
        });
    }

    [Fact]
    public async Task LinesUpTheValuesOfItsRows()
    {
        await _builder.Run(() =>
        {
            ContentPresenter shortValue = PanelCardTestsBuilder.Part<ContentPresenter>(_builder.Short, "PART_ContentPresenter");
            ContentPresenter longValue = PanelCardTestsBuilder.Part<ContentPresenter>(_builder.Long, "PART_ContentPresenter");
            TextBlock longLabel = PanelCardTestsBuilder.Part<TextBlock>(_builder.Long, "PART_Label");

            _builder.Resize(EditorLayout.DefaultSideWidth);

            double shortLeft = _builder.LeftOf(shortValue);
            double longLeft = _builder.LeftOf(longValue);

            shortLeft.Should().Be(longLeft, "the card's labels share one column");
            double column = longLeft - _builder.LeftOf(_builder.Long);
            column.Should().BeGreaterThan(PanelRow.LabelColumn, "the column grows to the card's longest label");
            PanelCardTestsBuilder.IsCut(longLabel).Should().BeFalse("no label is cut at the side panel's default width (rule R4)");
        });
    }

    [Fact]
    public async Task KeepsAColumnOfShortLabelsAtItsStartingWidth()
    {
        await _builder.Run(() =>
        {
            ContentPresenter value = PanelCardTestsBuilder.Part<ContentPresenter>(_builder.Short, "PART_ContentPresenter");
            _builder.Long.Label = "Seed";
            PanelCardTestsBuilder.Settle();

            double column = _builder.LeftOf(value) - _builder.LeftOf(_builder.Short);

            column.Should().Be(PanelRow.LabelColumn);
        });
    }

    [Fact]
    public async Task StretchesItsValuesToThePanelsEdge()
    {
        await _builder.Run(() =>
        {
            TextBox value = (TextBox)_builder.Short.Content!;
            double right = _builder.LeftOf(value) + value.Bounds.Width;
            double edge = _builder.LeftOf(_builder.Card) + _builder.Card.Bounds.Width;

            right.Should().BeApproximately(edge - 8, 0.5, "the body keeps 8 px at its right");
        });
    }

    [Fact]
    public async Task KeepsItsValuesAtThePanelsEdgeAsThePanelIsDraggedWiderAndNarrower()
    {
        await _builder.Run(() =>
        {
            TextBox value = (TextBox)_builder.Short.Content!;
            TextBox other = (TextBox)_builder.Long.Content!;

            _builder.Resize(380);

            RightIn(value).Should().BeApproximately(380 - 8, 0.5, "the value grows with the panel");
            RightIn(other).Should().BeApproximately(380 - 8, 0.5);

            _builder.Resize(240);

            RightIn(value).Should().BeApproximately(240 - 8, 0.5, "the value shrinks with the panel");
            RightIn(other).Should().BeApproximately(240 - 8, 0.5);
        });
    }

    [Fact]
    public async Task CutsALongLabelBeforeAValueGetsNarrowerThanItNeeds()
    {
        await _builder.Run(() =>
        {
            TextBox value = (TextBox)_builder.Long.Content!;
            TextBlock label = PanelCardTestsBuilder.Part<TextBlock>(_builder.Long, "PART_Label");

            _builder.Resize(240);

            PanelCardTestsBuilder.IsCut(label).Should().BeTrue("the side panel at its narrowest has no room for the whole label");
            value.Bounds.Width.Should().BeInRange(PanelRow.ValueWidth, PanelRow.ValueWidth + 8, "labels give way, a letter at a time, so a value keeps its width");

            _builder.Resize(EditorLayout.DefaultSideWidth);

            PanelCardTestsBuilder.IsCut(label).Should().BeFalse("the label comes back as the panel is dragged wider");
        });
    }

    [Fact]
    public async Task ShowsACutLabelWholeInItsTooltipAndLeavesTheRowsHelpOverAWholeOne()
    {
        await _builder.Run(() =>
        {
            TextBlock label = PanelCardTestsBuilder.Part<TextBlock>(_builder.Long, "PART_Label");
            ToolTip.SetTip(_builder.Long, "What the value does.");
            _builder.Resize(EditorLayout.DefaultSideWidth);

            ToolTip.GetTip(label).Should().BeNull("the row's help shows over a label that reads in full");

            _builder.Resize(240);

            ToolTip.GetTip(label).Should().Be(PanelCardTestsBuilder.LongLabel);
        });
    }

    /// <summary>How far the control's right edge is from the card's left edge.</summary>
    private double RightIn(Control control)
    {
        return _builder.LeftOf(control) + control.Bounds.Width - _builder.LeftOf(_builder.Card);
    }

    [Fact]
    public async Task PutsANestedRowsValueUnderItsLabel()
    {
        await _builder.Run(() =>
        {
            ContentPresenter value = PanelCardTestsBuilder.Part<ContentPresenter>(_builder.Short, "PART_ContentPresenter");
            TextBlock label = PanelCardTestsBuilder.Part<TextBlock>(_builder.Short, "PART_Label");

            _builder.Short.IsNested = true;
            PanelCardTestsBuilder.Settle();

            double top = value.TranslatePoint(default, _builder.Short)!.Value.Y;
            top.Should().BeGreaterThanOrEqualTo(label.Bounds.Bottom, "a value holding rows of its own goes under its label");
            double indent = _builder.LeftOf(value) - _builder.LeftOf(_builder.Short);
            indent.Should().Be(10);
        });
    }
}
