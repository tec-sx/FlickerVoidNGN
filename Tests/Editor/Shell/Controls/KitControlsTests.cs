using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Controls.Lists;
using FlickerVoid.Editor.Controls.Mix;
using FlickerVoid.Editor.Controls.Timelines;
using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Tests.Controls;

public sealed class KitControlsTests
{
    private readonly KitControlsTestsBuilder _builder = new();

    [Fact]
    public async Task PlaysPausesAndStopsFromTheTransport()
    {
        await _builder.Run(() => new Transport { Source = _builder.Transport, Width = 600 }, transport =>
        {
            Button play = KitControlsTestsBuilder.Part<Button>(transport, "PART_Play");
            Button stop = KitControlsTestsBuilder.Part<Button>(transport, "PART_Stop");

            transport.Bounds.Height.Should().Be(34);
            play.Bounds.Width.Should().Be(22);
            play.Bounds.Height.Should().Be(22);

            _builder.Click(play);
            _builder.Transport.IsPlaying.Should().BeTrue();

            _builder.Click(stop);
            _builder.Transport.Stops.Should().Be(1);
            _builder.Transport.IsPlaying.Should().BeFalse();
        });
    }

    [Fact]
    public async Task ShowsThePositionAndTheLoop()
    {
        await _builder.Run(() => new Transport { Source = _builder.Transport, Width = 600 }, transport =>
        {
            TextBlock position = KitControlsTestsBuilder.Part<TextBlock>(transport, "PART_Position");
            ToggleButton loop = KitControlsTestsBuilder.Part<ToggleButton>(transport, "PART_Loop");

            position.Text.Should().Be("6.2.1");
            position.FontSize.Should().Be(15);

            _builder.Click(loop);
            _builder.Transport.IsLooping.Should().BeTrue();

            transport.ShowsLoop = false;
            KitControlsTestsBuilder.Settle();
            loop.IsVisible.Should().BeFalse();
        });
    }

    [Fact]
    public async Task DrawsATriggerButtonForEachState()
    {
        await _builder.Run(() => new TriggerButton { Content = "Street", Key = "1", Tint = Color.Parse("#56D1A3") }, trigger =>
        {
            Border key = KitControlsTestsBuilder.Part<Border>(trigger, "PART_Key");
            PathIcon icon = KitControlsTestsBuilder.Part<PathIcon>(trigger, "PART_Icon");

            trigger.Bounds.Height.Should().Be(24);
            key.Bounds.Width.Should().Be(16);
            key.Bounds.Height.Should().Be(16);
            icon.IsVisible.Should().BeFalse("an idle trigger without an icon of its own shows none");

            trigger.State = RegionState.Live;
            KitControlsTestsBuilder.Settle();
            icon.Data.Should().BeSameAs(LucideIcons.Repeat);
            ((ISolidColorBrush)trigger.Ink!).Color.Should().Be((Color)trigger.FindResource("EditorCueInk")!, "a live trigger is filled, in dark ink");

            trigger.State = RegionState.Queued;
            KitControlsTestsBuilder.Settle();
            icon.Data.Should().BeSameAs(LucideIcons.Hourglass);
            trigger.Padding.Should().Be(new Thickness(3, 0, 9, 0), "the 2 px dashed border takes a pixel from each side");
        });
    }

    [Fact]
    public async Task ChoosesOneTabOfAButtonGroup()
    {
        await _builder.Run(() => new ButtonGroup { ItemsSource = new[] { "Mixer", "Clip", "Live" }, SelectedIndex = 0 }, group =>
        {
            ListBoxItem[] tabs = [.. group.GetRealizedContainers().OfType<ListBoxItem>()];

            tabs.Should().HaveCount(3);
            tabs[0].Bounds.Height.Should().Be(25, "a 26 px bar with its 1 px line under the tabs");
            ContentPresenter chosen = KitControlsTestsBuilder.Part<ContentPresenter>(tabs[0], "PART_ContentPresenter");
            chosen.BoxShadow.Should().Be(group.FindResource("EditorTabBottomBar"), "the chosen tab has an accent bar at its foot");

            _builder.Click(tabs[2]);

            group.SelectedIndex.Should().Be(2);
        });
    }

    [Fact]
    public async Task HasTheMixersStripLayout()
    {
        await _builder.Run(() => new ChannelStrip { Title = "Bass synth", Height = 230, Level = 0.5, Tint = Brushes.Orange }, strip =>
        {
            Border name = KitControlsTestsBuilder.Part<Border>(strip, "PART_Name");
            ToggleButton mute = KitControlsTestsBuilder.Part<ToggleButton>(strip, "PART_Mute");

            strip.Bounds.Width.Should().Be(88);
            name.Bounds.Height.Should().Be(22);
            name.BorderThickness.Top.Should().Be(3);
            mute.Bounds.Width.Should().Be(18);
            mute.Bounds.Height.Should().Be(16);

            _builder.Click(mute);
            strip.IsMuted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task FadesAStripMarkedSilentButNotOneBetweenBeats()
    {
        await _builder.Run(() => new ChannelStrip { Title = "Drums", Height = 230, Level = 0 }, strip =>
        {
            strip.Opacity.Should().Be(1, "a quiet moment of a layer that plays is not silence");

            strip.IsSilent = true;
            KitControlsTestsBuilder.Settle();
            strip.Opacity.Should().Be(0.55);
            strip.Level = 0.5;
            KitControlsTestsBuilder.Settle();
            strip.Opacity.Should().Be(0.55, "the meter does not light a strip its mixer says is silent");
        });
    }

    [Fact]
    public async Task HasALaneHeadersEdgeAndIndentsASubLane()
    {
        await _builder.Run(() => new LaneHeader { Title = "Bass synth", Width = 250, Height = 84, Tint = Brushes.Orange }, header =>
        {
            header.Content = new TextBlock { Text = "Always" };
            KitControlsTestsBuilder.Settle();
            Border edge = KitControlsTestsBuilder.Part<Border>(header, "PART_Edge");
            TextBlock title = KitControlsTestsBuilder.Part<TextBlock>(header, "PART_Title");
            ContentPresenter content = KitControlsTestsBuilder.Part<ContentPresenter>(header, "PART_ContentPresenter");

            edge.Bounds.Width.Should().Be(3);
            title.FontWeight.Should().Be(FontWeight.SemiBold);
            content.IsVisible.Should().BeTrue();

            header.IsSub = true;
            KitControlsTestsBuilder.Settle();
            double indent = title.TranslatePoint(default, header)!.Value.X;
            indent.Should().Be(LaneHeader.SubIndent, "a lane under another starts 22 px in");
            content.IsVisible.Should().BeFalse();
        });
    }

    [Fact]
    public async Task SelectsSeveralRowsOfAnItemList()
    {
        await _builder.Run(() => new ItemList { ItemsSource = _builder.Rows, Width = 200 }, list =>
        {
            ListBoxItem[] rows = [.. list.GetRealizedContainers().OfType<ListBoxItem>()];

            _builder.Click(rows[0]);
            _builder.Click(rows[2], modifiers: RawInputModifiers.Control);
            list.SelectedRows.Should().Equal("a", "c");

            _builder.Click(rows[3], modifiers: RawInputModifiers.Shift);
            list.SelectedRows.Should().Equal("c", "d");

            _builder.Click(rows[1], MouseButton.Right);
            list.SelectedRows.Should().Equal("b");
            list.SelectedItems!.Cast<object>().Should().Equal("b");
        });
    }

    [Fact]
    public async Task SelectsEveryRowWithControlA()
    {
        await _builder.Run(() => new ItemList { ItemsSource = _builder.Rows, Width = 200 }, list =>
        {
            ListBoxItem[] rows = [.. list.GetRealizedContainers().OfType<ListBoxItem>()];
            _builder.Click(rows[0]);

            _builder.PressKey(PhysicalKey.A, RawInputModifiers.Control);

            list.SelectedRows.Should().Equal("a", "b", "c", "d");
        });
    }

    [Fact]
    public async Task ChoosesAPillOrAddsOne()
    {
        await _builder.Run(() => new PillFilter { ItemsSource = _builder.Pills, AddCommand = _builder.AddCommand, Width = 300 }, pills =>
        {
            ListBoxItem[] items = [.. pills.GetRealizedContainers().OfType<ListBoxItem>()];

            items[1].Bounds.Height.Should().Be(20);
            items[1].CornerRadius.Should().Be(new CornerRadius(10));

            _builder.Click(items[1]);
            pills.SelectedIndex.Should().Be(1);

            _builder.Click(items[2]);
            _builder.Adds.Should().Be(1);
            pills.SelectedIndex.Should().Be(1, "the add pill is never chosen");
            items[2].Classes.Should().Contain("add");
        });
    }
}
