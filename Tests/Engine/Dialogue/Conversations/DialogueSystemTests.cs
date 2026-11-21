using Arch.Core;

using FlickerVoid.Engine.Dialogue.Barks;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Dialogue.Tests.Conversations;

public sealed class DialogueSystemTests : IDisposable
{
    private const string Greeting = """{ "$type": "line", "id": "hello", "text": "Welcome to the Fox." }""";
    private const string Reply = """{ "$type": "line", "id": "reply", "speaker": "Character.Hero", "text": "Thanks." }""";
    private const string End = """{ "$type": "finish", "id": "end" }""";
    private const string Start = """{ "$type": "start", "id": "start" }""";

    private static readonly Tag Asked = new("Fact.Inn.Asked");
    private static readonly Tag Coins = new("Fact.Hero.Coins");

    private readonly DialogueSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AConversationPlaysItsLinesAndEndsWithItsGraph()
    {
        _builder.WithGraph($"{Start}, {Greeting}, {Reply}, {End}", """
            { "from": "start", "to": "hello" }, { "from": "hello", "to": "reply" }, { "from": "reply", "to": "end" }
            """).Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        List<LineStartedEvent> lines = _builder.Collect<LineStartedEvent>();
        List<ConversationEndedEvent> ended = _builder.Collect<ConversationEndedEvent>();

        bool started = _builder.Dialogue.Start(hero, innkeeper);
        _builder.Wait(0.1);
        bool talking = _builder.Dialogue.IsInConversation;
        _builder.Wait(3);

        started.Should().BeTrue();
        talking.Should().BeTrue();
        lines.Select(sent => (sent.Line.Text, sent.Line.SpeakerEntity)).Should().Equal(("Welcome to the Fox.", innkeeper), ("Thanks.", hero));
        lines[0].Line.Duration.Should().Be(1);
        ended.Should().ContainSingle();
        _builder.Dialogue.IsInConversation.Should().BeFalse();
    }

    [Fact]
    public void OnlyOneConversationRunsAtATime()
    {
        _builder.WithGraph($"{Start}, {Greeting}", """{ "from": "start", "to": "hello" }""").Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        _builder.Dialogue.Start(hero, innkeeper);

        bool second = _builder.Dialogue.Start(hero, innkeeper);

        second.Should().BeFalse();
    }

    [Fact]
    public void AdvanceSkipsALineAndALineWaitingForInputWaitsForIt()
    {
        string waiting = """{ "$type": "line", "id": "reply", "text": "Take your time.", "waitForInput": true }""";
        _builder.WithGraph($"{Start}, {Greeting}, {waiting}", """
            { "from": "start", "to": "hello" }, { "from": "hello", "to": "reply" }
            """).Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        _builder.Dialogue.Start(hero, innkeeper);

        _builder.Dialogue.Advance();
        string second = _builder.Dialogue.CurrentLine.Text;
        _builder.Wait(5);
        string stillSecond = _builder.Dialogue.CurrentLine.Text;
        _builder.Dialogue.Advance();

        second.Should().Be("Take your time.");
        stillSecond.Should().Be("Take your time.");
        _builder.Dialogue.IsInConversation.Should().BeFalse();
    }

    [Fact]
    public void ChoicesHideOrLockTheOptionsWhoseConditionsFailAndFollowThePickedOne()
    {
        string choice = """
            { "$type": "choice", "id": "ask", "options": [
              { "text": "Any rumours?", "onChosen": { "effects": [ { "$type": "fact", "fact": "Fact.Inn.Asked" } ] } },
              { "text": "A room, please.", "conditions": { "conditions": [ { "$type": "fact", "fact": "Fact.Hero.Coins", "value": 5 } ], "failurePresentation": "ShowLockedWithReason", "failureReason": "Five coins." } },
              { "text": "Secret password.", "conditions": { "conditions": [ { "$type": "fact", "fact": "Fact.Inn.Password" } ] } }
            ] }
            """;
        _builder.WithGraph($"{Start}, {choice}, {Reply}", """
            { "from": "start", "to": "ask" }, { "from": "ask", "output": "First", "to": "reply" }
            """).Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        _builder.Game.Facts.SetFact(Coins, 2);
        List<ChoicesPresentedEvent> presented = _builder.Collect<ChoicesPresentedEvent>();
        _builder.Dialogue.Start(hero, innkeeper);
        _builder.Game.Events.Dispatch();

        bool locked = _builder.Dialogue.Choose(1);
        bool picked = _builder.Dialogue.Choose(0);

        presented.Should().ContainSingle();
        presented[0].Choices.Should().Equal(
            new DialogueChoice(0, "Any rumours?", true, ""),
            new DialogueChoice(1, "A room, please.", false, "Five coins."));
        locked.Should().BeFalse();
        picked.Should().BeTrue();
        _builder.Game.Facts.GetFact(Asked).Should().Be(1);
        _builder.Dialogue.CurrentLine.Text.Should().Be("Thanks.");
    }

    [Fact]
    public void AChoiceWithNothingAvailableEndsTheConversation()
    {
        string choice = """
            { "$type": "choice", "id": "ask", "options": [
              { "text": "Secret password.", "conditions": { "conditions": [ { "$type": "fact", "fact": "Fact.Inn.Password" } ] } }
            ] }
            """;
        _builder.WithGraph($"{Start}, {choice}", """{ "from": "start", "to": "ask" }""").Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();

        _builder.Dialogue.Start(hero, innkeeper);

        _builder.Dialogue.IsInConversation.Should().BeFalse();
    }

    [Fact]
    public void EndingAConversationStopsItsGraph()
    {
        string after = """{ "$type": "applyEffects", "id": "after", "effects": { "effects": [ { "$type": "fact", "fact": "Fact.Inn.Asked" } ] } }""";
        _builder.WithGraph($"{Start}, {Greeting}, {after}", """
            { "from": "start", "to": "hello" }, { "from": "hello", "to": "after" }
            """).Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        _builder.Dialogue.Start(hero, innkeeper);

        _builder.Dialogue.End();
        _builder.Wait(3);

        _builder.Dialogue.IsInConversation.Should().BeFalse();
        _builder.Game.Facts.GetFact(Asked).Should().Be(0);
    }

    [Fact]
    public void TheConversationEndsWhenItsOwnerIsGone()
    {
        _builder.WithGraph($"{Start}, {Greeting}", """{ "from": "start", "to": "hello" }""").Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        _builder.Dialogue.Start(hero, innkeeper);

        _builder.World.Destroy(innkeeper);
        _builder.Wait(0.25);

        _builder.Dialogue.IsInConversation.Should().BeFalse();
    }

    [Fact]
    public void TheStartConversationEffectTalksToTheTarget()
    {
        _builder.WithGraph($"{Start}, {Greeting}", """{ "from": "start", "to": "hello" }""").Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        Effect talk = _builder.Read<Effect>("""{ "$type": "startConversation" }""");

        talk.Apply(_builder.Context(hero, innkeeper));

        _builder.Dialogue.IsInConversation.Should().BeTrue();
        _builder.Dialogue.Owner.Should().Be(innkeeper);
        _builder.Dialogue.Instigator.Should().Be(hero);
    }

    [Fact]
    public void BarksPickAFittingLineThatWasNotSaidLast()
    {
        _builder.WithFile("Barks.fvtable", """
            { "type": "bark", "rows": {
              "Greet": { "text": "Evening." },
              "Grumble": { "text": "Busy night.", "requiredTags": [ "State.Tired" ] },
              "Guard": { "text": "Move along.", "identityTag": "Role.Guard" }
            } }
            """)
            .WithGraph($$"""{{Start}}, { "$type": "bark", "id": "bark", "barks": "Barks.fvtable" }, {{End}}""", """{ "from": "start", "to": "bark" }, { "from": "bark", "to": "end" }""")
            .Build();
        (Entity hero, Entity innkeeper) = _builder.Pair();
        _builder.World.Set(innkeeper, new OwnedTags([new Tag("State.Tired")]));
        List<BarkEvent> barks = _builder.Collect<BarkEvent>();

        for (int index = 0; index < 4; index++)
        {
            _builder.Dialogue.Start(hero, innkeeper);
            _builder.Wait(3.5);
        }

        List<string> texts = barks.Select(bark => bark.Text).ToList();
        texts.Should().HaveCount(4).And.OnlyContain(text => text == "Evening." || text == "Busy night.");
        for (int index = 1; index < texts.Count; index++)
        {
            texts[index].Should().NotBe(texts[index - 1]);
        }

        barks.Should().OnlyContain(bark => bark.Speaker == innkeeper && bark.Duration == 3);
    }
}
