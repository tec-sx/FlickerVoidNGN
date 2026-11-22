using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Interface.Hud;

namespace FlickerVoid.Interface.Tests.Hud;

public sealed class HudViewModelTests : IDisposable
{
    private readonly HudViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TracksActiveQuestsWithTheirCurrentObjective()
    {
        HudViewModel hud = _builder.Build();

        _builder.StartQuest("Quest.Cellar");

        hud.Quests.Should().ContainSingle();
        hud.Quests[0].Name.Should().Be("The cellar key");
        hud.Quests[0].Objective.Should().Be("Find the cellar key");
    }

    [Fact]
    public void TracksAtMostThreeQuestsTheLatestChangedFirst()
    {
        HudViewModel hud = _builder.WithQuests(4).Build();

        _builder.StartQuest("Quest.Cellar");
        for (int index = 1; index <= 4; index++)
        {
            _builder.StartQuest($"Quest.Extra{index}");
        }

        hud.Quests.Select(quest => quest.Name).Should().Equal("Extra 4", "Extra 3", "Extra 2");
    }

    [Fact]
    public void LeavesFinishedQuestsOut()
    {
        HudViewModel hud = _builder.Build();
        _builder.StartQuest("Quest.Cellar");

        _builder.Quests.SetState(new Tag("Quest.Cellar"), QuestState.Completed);
        _builder.Game.Events.Dispatch();

        hud.Quests.Should().BeEmpty();
    }

    [Fact]
    public void ShowsTheConversationsLineAndSpeaker()
    {
        HudViewModel hud = _builder.Build();

        _builder.Talk();

        hud.Dialogue.IsOpen.Should().BeTrue();
        hud.Dialogue.Speaker.Should().Be("Innkeeper");
        hud.Dialogue.Line.Should().Be("Evening, fox.");
        hud.Dialogue.CanAdvance.Should().BeTrue();
    }

    [Fact]
    public void OffersTheChoicesNumberedAndPicksThem()
    {
        HudViewModel hud = _builder.Build();
        _builder.Talk();

        hud.Dialogue.AdvanceCommand.Execute(null);
        _builder.Game.Events.Dispatch();

        hud.Dialogue.Choices.Select(choice => choice.Label).Should().Equal("1. Anything I can do for you?", "2. Goodbye.");
        hud.Dialogue.CanAdvance.Should().BeFalse();
        hud.Dialogue.Choices[0].ChooseCommand.Execute(null);
        _builder.Game.Events.Dispatch();
        hud.Dialogue.Line.Should().Be("Find my key.");
        hud.Dialogue.Choices.Should().BeEmpty();
    }

    [Fact]
    public void ClosesWhenTheConversationEnds()
    {
        HudViewModel hud = _builder.Build();
        _builder.Talk();

        hud.Dialogue.LeaveCommand.Execute(null);
        _builder.Game.Events.Dispatch();

        hud.Dialogue.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void PromptsTheOffersOfWhatThePointerIsOver()
    {
        HudViewModel hud = _builder.Build();

        hud.Prompt.Update(_builder.Innkeeper);

        hud.Prompt.IsVisible.Should().BeTrue();
        hud.Prompt.Title.Should().Be("Innkeeper");
        hud.Prompt.Offers.Select(offer => offer.Name).Should().Equal("Talk");
    }

    [Fact]
    public void HidesThePromptAwayFromInteractablesAndDuringConversations()
    {
        HudViewModel hud = _builder.Build();
        hud.Prompt.Update(_builder.Innkeeper);

        hud.Prompt.Update(Entity.Null);
        bool away = hud.Prompt.IsVisible;
        _builder.Talk();
        hud.Prompt.Update(_builder.Innkeeper);
        bool talking = hud.Prompt.IsVisible;

        away.Should().BeFalse();
        talking.Should().BeFalse();
    }
}
