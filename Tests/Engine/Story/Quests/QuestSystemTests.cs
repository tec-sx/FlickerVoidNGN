using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Story.Quests;

namespace FlickerVoid.Engine.Story.Tests.Quests;

public sealed class QuestSystemTests : IDisposable
{
    private static readonly Tag Cellar = new("Quest.Cellar");
    private static readonly Tag Key = new("Quest.Cellar.FoundKey");
    private static readonly Tag Door = new("Quest.Cellar.OpenedDoor");
    private static readonly Tag Wine = new("Quest.Cellar.TastedWine");
    private static readonly Tag HasKey = new("Fact.Cellar.HasKey");
    private static readonly Tag DoorOpen = new("Fact.Cellar.DoorOpen");
    private static readonly Tag Reward = new("Fact.Cellar.Reward");
    private static readonly Tag Rumour = new("Fact.Cellar.Rumour");

    private const string Objectives = """
        "objectives": [
          { "fact": "Quest.Cellar.FoundKey", "completeWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.Cellar.HasKey" } ] } },
          { "fact": "Quest.Cellar.OpenedDoor", "completeWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.Cellar.DoorOpen" } ] },
            "visibleWhen": { "conditions": [ { "$type": "fact", "fact": "Quest.Cellar.FoundKey" } ] } },
          { "fact": "Quest.Cellar.TastedWine", "optional": true }
        ]
        """;

    private readonly QuestSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AQuestStartsOnceAndAppliesItsStartEffects()
    {
        _builder.WithQuest(Cellar.Name, """
            "onStarted": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Rumour", "write": "Add" } ] }
            """).Build();

        bool started = _builder.Quests.Start(Cellar);
        bool again = _builder.Quests.Start(Cellar);

        started.Should().BeTrue();
        again.Should().BeFalse();
        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Active);
        _builder.Game.Facts.GetFact(Cellar).Should().Be((int)QuestState.Active);
        _builder.Game.Facts.GetFact(Rumour).Should().Be(1);
    }

    [Fact]
    public void AQuestStartsByItselfWhenItsStartConditionsPass()
    {
        _builder.WithQuest(Cellar.Name, """
            "autoStartWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.Cellar.Rumour" } ] }
            """).Build();

        QuestState before = _builder.Quests.GetState(Cellar);
        _builder.Game.Facts.SetFact(Rumour, 1);

        before.Should().Be(QuestState.NotStarted);
        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Active);
    }

    [Fact]
    public void AQuestAlreadyActiveWhenPlayStartsIsKeptActive()
    {
        _builder.WithQuest(Cellar.Name, Objectives);
        _builder.Game.Facts.SetFact(Cellar, (int)QuestState.Active);
        _builder.Game.Facts.SetFact(HasKey, 1);
        _builder.Build();

        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Active);
        _builder.Game.Facts.GetFact(Key).Should().Be(1);
    }

    [Fact]
    public void ObjectivesCompleteWhenTheirConditionsPassAndTheQuestWhenTheRequiredOnesAreDone()
    {
        _builder.WithQuest(Cellar.Name, Objectives + """
            , "onCompleted": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Reward" } ] }
            """).Build();
        _builder.Quests.Start(Cellar);

        _builder.Game.Facts.SetFact(HasKey, 1);
        QuestState halfway = _builder.Quests.GetState(Cellar);
        _builder.Game.Facts.SetFact(DoorOpen, 1);

        halfway.Should().Be(QuestState.Active);
        _builder.Quests.IsObjectiveComplete(Key).Should().BeTrue();
        _builder.Quests.IsObjectiveComplete(Wine).Should().BeFalse();
        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Completed);
        _builder.Game.Facts.GetFact(Reward).Should().Be(1);
    }

    [Fact]
    public void AHiddenObjectiveWaitsUntilItIsVisible()
    {
        _builder.WithQuest(Cellar.Name, Objectives).Build();
        _builder.Quests.Start(Cellar);

        _builder.Game.Facts.SetFact(DoorOpen, 1);
        QuestDefinition quest = _builder.Quests.GetQuests(QuestState.Active).Single();
        bool visible = _builder.Quests.IsObjectiveVisible(quest.Objectives[1]);

        visible.Should().BeFalse();
        _builder.Quests.IsObjectiveComplete(Door).Should().BeFalse();
    }

    [Fact]
    public void AQuestOfOnlyOptionalObjectivesNeverCompletesByItself()
    {
        _builder.WithQuest(Cellar.Name, """
            "objectives": [ { "fact": "Quest.Cellar.TastedWine", "optional": true } ]
            """).Build();
        _builder.Quests.Start(Cellar);

        _builder.Game.Facts.SetFact(Wine, 1);

        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Active);
    }

    [Fact]
    public void AnActiveQuestFailsWhenItsFailConditionsPass()
    {
        _builder.WithQuest(Cellar.Name, Objectives + """
            , "failWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.Cellar.Rumour", "value": 3 } ] },
            "onFailed": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Reward", "value": -1 } ] }
            """).Build();
        _builder.Quests.Start(Cellar);

        _builder.Game.Facts.SetFact(Rumour, 3);
        bool completed = _builder.Quests.Complete(Cellar);

        completed.Should().BeFalse();
        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Failed);
        _builder.Game.Facts.GetFact(Reward).Should().Be(-1);
    }

    [Fact]
    public void QuestsWhoseEffectsStartEachOtherSettleInOneChange()
    {
        _builder.WithQuest("Quest.First", """
            "onStarted": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Rumour" } ] }
            """)
            .WithQuest("Quest.Second", """
                "autoStartWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.Cellar.Rumour" } ] },
                "onStarted": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Reward" } ] }
                """)
            .WithQuest("Quest.Third", """
                "autoStartWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.Cellar.Reward" } ] }
                """)
            .Build();

        _builder.Quests.Start(new Tag("Quest.First"));

        _builder.Quests.GetQuests(QuestState.Active).Select(quest => quest.Id.Name).Should().BeEquivalentTo(["Quest.First", "Quest.Second", "Quest.Third"]);
    }

    [Fact]
    public void EveryChangeOfAQuestIsSent()
    {
        _builder.WithQuest(Cellar.Name, Objectives).Build();
        List<QuestChangedEvent> changed = _builder.Collect<QuestChangedEvent>();

        _builder.Quests.Start(Cellar);
        _builder.Game.Facts.SetFact(HasKey, 1);
        _builder.Quests.Fail(Cellar);
        _builder.Game.Events.Dispatch();

        changed.Should().Equal(
            new QuestChangedEvent(Cellar, QuestState.Active),
            new QuestChangedEvent(Cellar, QuestState.Active),
            new QuestChangedEvent(Cellar, QuestState.Failed));
    }

    [Fact]
    public void AQuestRunsItsFlowWhileItIsActive()
    {
        _builder.WithFile("Quests/Cellar.fvflow", """
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "waitForConditions", "id": "door", "conditions": { "conditions": [ { "$type": "fact", "fact": "Fact.Cellar.DoorOpen" } ] } },
                { "$type": "completeObjective", "id": "done", "objective": "Quest.Cellar.OpenedDoor" },
                { "$type": "setQuestState", "id": "complete", "quest": "Quest.Cellar", "state": "Completed" }
              ],
              "links": [ { "from": "start", "to": "door" }, { "from": "door", "to": "done" }, { "from": "done", "to": "complete" } ]
            }
            """)
            .WithQuest(Cellar.Name, """ "flow": "Quests/Cellar.fvflow" """)
            .Build();

        _builder.Quests.Start(Cellar);
        bool running = _builder.Quests.IsFlowRunning(Cellar);
        _builder.Game.Facts.SetFact(DoorOpen, 1);

        running.Should().BeTrue();
        _builder.Game.Facts.GetFact(Door).Should().Be(1);
        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Completed);
        _builder.Quests.IsFlowRunning(Cellar).Should().BeFalse();
    }

    [Fact]
    public void AFailedQuestStopsItsFlow()
    {
        _builder.WithFile("Quests/Cellar.fvflow", """
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "waitForQuestState", "id": "wait", "quest": "Quest.Cellar", "state": "Completed" },
                { "$type": "applyEffects", "id": "reward", "effects": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Reward" } ] } }
              ],
              "links": [ { "from": "start", "to": "wait" }, { "from": "wait", "to": "reward" } ]
            }
            """)
            .WithQuest(Cellar.Name, """ "flow": "Quests/Cellar.fvflow" """)
            .Build();
        _builder.Quests.Start(Cellar);

        _builder.Quests.Fail(Cellar);

        _builder.Quests.IsFlowRunning(Cellar).Should().BeFalse();
        _builder.Game.Facts.GetFact(Reward).Should().Be(0);
    }

    [Fact]
    public void FlowsWaitForAQuestStateAndCheckItWithTheQuestStateCondition()
    {
        _builder.WithFile("Quests/Waiter.fvflow", """
            {
              "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "waitForQuestState", "id": "wait", "quest": "Quest.Cellar", "state": "Completed" },
                { "$type": "branch", "id": "check", "conditions": { "conditions": [ { "$type": "questState", "quest": "Quest.Cellar", "state": "Completed" } ] } },
                { "$type": "applyEffects", "id": "reward", "effects": { "effects": [ { "$type": "fact", "fact": "Fact.Cellar.Reward" } ] } }
              ],
              "links": [ { "from": "start", "to": "wait" }, { "from": "wait", "to": "check" }, { "from": "check", "output": "True", "to": "reward" } ]
            }
            """)
            .WithQuest("Quest.Waiter", """ "flow": "Quests/Waiter.fvflow" """)
            .WithQuest(Cellar.Name)
            .Build();
        _builder.Quests.Start(new Tag("Quest.Waiter"));
        _builder.Quests.Start(Cellar);

        int before = _builder.Game.Facts.GetFact(Reward);
        _builder.Quests.Complete(Cellar);

        before.Should().Be(0);
        _builder.Game.Facts.GetFact(Reward).Should().Be(1);
    }

    [Fact]
    public void TheSetQuestStateEffectMovesAQuestOn()
    {
        _builder.WithQuest(Cellar.Name)
            .WithQuest("Quest.Starter", """
                "onStarted": { "effects": [ { "$type": "setQuestState", "quest": "Quest.Cellar", "state": "Active" } ] }
                """)
            .Build();

        _builder.Quests.Start(new Tag("Quest.Starter"));

        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Active);
    }

    [Fact]
    public void ALoadRunsTheFlowsOfTheQuestsActiveInTheSave()
    {
        string flow = """
            {
              "nodes": [ { "$type": "start", "id": "start" }, { "$type": "waitForConditions", "id": "wait", "conditions": { "conditions": [ { "$type": "fact", "fact": "Fact.Never" } ] } } ],
              "links": [ { "from": "start", "to": "wait" } ]
            }
            """;
        _builder.WithFile("Quests/Cellar.fvflow", flow)
            .WithFile("Quests/Later.fvflow", flow)
            .WithQuest(Cellar.Name, """ "flow": "Quests/Cellar.fvflow" """)
            .WithQuest("Quest.Later", """ "flow": "Quests/Later.fvflow" """)
            .Build();
        _builder.Quests.Start(Cellar);
        _builder.Saves.Save("Slot");
        _builder.Quests.Fail(Cellar);
        _builder.Quests.Start(new Tag("Quest.Later"));

        _builder.Saves.Load("Slot");

        _builder.Quests.GetState(Cellar).Should().Be(QuestState.Active);
        _builder.Quests.IsFlowRunning(Cellar).Should().BeTrue();
        _builder.Quests.IsFlowRunning(new Tag("Quest.Later")).Should().BeFalse();
    }

    [Fact]
    public void QuestRulesAreAppliedForThePlayer()
    {
        _builder.WithQuest(Cellar.Name).Build();
        IEntityView player = Substitute.For<IEntityView>();
        CaptureEffect capture = new();
        _builder.Data.Find<QuestDefinition>(Cellar)!.OnStarted.Effects.Add(capture);
        _builder.Quests.Player = player;

        _builder.Quests.Start(Cellar);

        capture.Instigator.Should().BeSameAs(player);
    }

    /// <summary>Remembers who it was applied for.</summary>
    private sealed class CaptureEffect : Effect
    {
        public IEntityView? Instigator { get; private set; }

        public override void Apply(ConditionContext context) => Instigator = context.Instigator;
    }
}
