using System.Diagnostics.CodeAnalysis;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>
/// Runs the quests of the content folder: starts them by their conditions or rules, completes their objectives and them as facts
/// change, fails them, and runs each active quest's flow graph. A quest's state lives in its fact, so a save holds it, and the save
/// keeps where each flow was; the system starts with the game's first step and after each load resumes the active quests' flows there.
/// </summary>
public sealed partial class QuestSystem : BaseSystem<World, GameTime>
{
    /// <summary>How often one evaluation goes over the quests while their effects keep changing each other.</summary>
    public const int MaxPasses = 8;

    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly EventBus _events;
    private readonly ILogger<QuestSystem> _logger;
    private readonly Dictionary<Tag, FlowRun> _flows = [];
    private SaveSystem? _saves;
    private IEntityView? _player;
    private Dictionary<string, List<string>>? _savedFlows;
    private bool _started;
    private bool _evaluating;
    private bool _dirty;
    private bool _loading;

    public QuestSystem(Game game)
        : base(game.World)
    {
        _facts = game.Facts;
        _services = game.Services;
        _events = game.Events;
        _logger = game.LoggerFactory.CreateLogger<QuestSystem>();
        game.SaveStates.AddGlobal<SavedQuestFlows>("questFlows", SaveFlows, RestoreFlows);
    }

    /// <summary>
    /// Who quest rules are checked for and effects applied to, as their instigator: the player, which the game sets. Running flows
    /// follow a new one, such as the player a scene travel spawned again.
    /// </summary>
    public IEntityView? Player
    {
        get => _player;
        set
        {
            _player = value;
            ConditionContext context = CreateContext();
            foreach (FlowRun run in _flows.Values)
            {
                run.SetConditions(context);
            }
        }
    }

    public QuestState GetState(Tag quest)
    {
        int value = _facts.GetFact(quest);
        return (QuestState)value;
    }

    public bool IsObjectiveComplete(Tag objective) => _facts.GetFact(objective) > 0;

    public bool IsObjectiveVisible(QuestObjective objective)
    {
        if (objective.VisibleWhen.IsEmpty)
        {
            return true;
        }

        ConditionContext context = CreateContext();
        return objective.VisibleWhen.Evaluate(context);
    }

    /// <summary>The quests of the content folder in <paramref name="state"/>.</summary>
    public IReadOnlyList<QuestDefinition> GetQuests(QuestState state)
    {
        List<QuestDefinition> found = [];
        foreach (QuestDefinition quest in LoadQuests())
        {
            if (GetState(quest.Id) == state)
            {
                found.Add(quest);
            }
        }

        return found;
    }

    public bool IsFlowRunning(Tag quest) => _flows.ContainsKey(quest);

    /// <summary>Starts a quest that has not started: applies its start effects and starts its flow.</summary>
    public bool Start(Tag quest)
    {
        if (!TrySetState(quest, QuestState.Active, QuestState.NotStarted, out QuestDefinition? definition))
        {
            return false;
        }

        ConditionContext context = CreateContext();
        definition.OnStarted.Apply(context);
        if (GetState(quest) == QuestState.Active)
        {
            StartFlow(definition);
        }

        Evaluate();
        return true;
    }

    /// <summary>Completes an active quest: applies its completion effects and stops its flow.</summary>
    public bool Complete(Tag quest)
    {
        if (!TrySetState(quest, QuestState.Completed, QuestState.Active, out QuestDefinition? definition))
        {
            return false;
        }

        ConditionContext context = CreateContext();
        definition.OnCompleted.Apply(context);
        StopFlow(quest);
        return true;
    }

    public bool Fail(Tag quest)
    {
        if (!TrySetState(quest, QuestState.Failed, QuestState.Active, out QuestDefinition? definition))
        {
            return false;
        }

        ConditionContext context = CreateContext();
        definition.OnFailed.Apply(context);
        StopFlow(quest);
        return true;
    }

    /// <summary>Starts, completes or fails the quest as <paramref name="state"/> says; false when the quest isn't in the state that allows it.</summary>
    public bool SetState(Tag quest, QuestState state)
    {
        switch (state)
        {
            case QuestState.Active:
                return Start(quest);
            case QuestState.Completed:
                return Complete(quest);
            case QuestState.Failed:
                return Fail(quest);
            default:
                return false;
        }
    }

    /// <summary>
    /// Starts, fails and completes quests and their objectives by their conditions. Called again while it runs (a quest's effects
    /// change facts), it goes over the quests once more afterwards, at most <see cref="MaxPasses"/> times.
    /// </summary>
    public void Evaluate()
    {
        if (_evaluating)
        {
            _dirty = true;
            return;
        }

        _evaluating = true;
        try
        {
            int pass = 0;
            do
            {
                _dirty = false;
                foreach (QuestDefinition quest in LoadQuests())
                {
                    EvaluateQuest(quest);
                }

                pass++;
            }
            while (_dirty && pass < MaxPasses);

            if (_dirty)
            {
                LogStillChanging(MaxPasses);
            }
        }
        finally
        {
            _evaluating = false;
        }
    }

    /// <summary>Starts following facts, evaluates the quests and starts the flows of the active ones, as play begins.</summary>
    public override void Initialize()
    {
        _started = true;
        _facts.FactChanged += HandleFactChanged;
        _saves = _services.GetService<SaveSystem>();
        if (_saves is not null)
        {
            _saves.Loading += HandleLoading;
            _saves.Loaded += HandleLoaded;
        }

        Evaluate();
        foreach (QuestDefinition quest in GetQuests(QuestState.Active))
        {
            StartFlow(quest);
        }
    }

    public override void Dispose()
    {
        if (_started)
        {
            _facts.FactChanged -= HandleFactChanged;
            if (_saves is not null)
            {
                _saves.Loading -= HandleLoading;
                _saves.Loaded -= HandleLoaded;
            }

            _started = false;
        }

        foreach (FlowRun run in _flows.Values.ToList())
        {
            run.Finish();
        }

        base.Dispose();
    }

    private IReadOnlyList<QuestDefinition> LoadQuests()
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null)
        {
            return [];
        }

        return data.LoadAll<QuestDefinition>();
    }

    private void EvaluateQuest(QuestDefinition quest)
    {
        ConditionContext context = CreateContext();
        QuestState state = GetState(quest.Id);
        if (state == QuestState.NotStarted)
        {
            if (!quest.AutoStartWhen.IsEmpty && quest.AutoStartWhen.Evaluate(context))
            {
                Start(quest.Id);
            }

            return;
        }

        if (state != QuestState.Active)
        {
            return;
        }

        if (!quest.FailWhen.IsEmpty && quest.FailWhen.Evaluate(context))
        {
            Fail(quest.Id);
            return;
        }

        if (UpdateObjectives(quest, context))
        {
            Notify(quest.Id);
        }

        if (AreRequiredObjectivesDone(quest))
        {
            Complete(quest.Id);
        }
    }

    private bool UpdateObjectives(QuestDefinition quest, ConditionContext context)
    {
        bool changed = false;
        foreach (QuestObjective objective in quest.Objectives)
        {
            if (IsObjectiveComplete(objective.Fact) || !IsObjectiveVisible(objective))
            {
                continue;
            }

            if (!objective.CompleteWhen.IsEmpty && objective.CompleteWhen.Evaluate(context))
            {
                _facts.SetFact(objective.Fact, 1);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>True when every required objective is done; a quest with none is completed only by a rule.</summary>
    private bool AreRequiredObjectivesDone(QuestDefinition quest)
    {
        bool anyRequired = false;
        foreach (QuestObjective objective in quest.Objectives)
        {
            if (objective.Optional)
            {
                continue;
            }

            anyRequired = true;
            if (!IsObjectiveComplete(objective.Fact))
            {
                return false;
            }
        }

        return anyRequired;
    }

    private bool TrySetState(Tag quest, QuestState state, QuestState required, [NotNullWhen(true)] out QuestDefinition? definition)
    {
        definition = null;
        if (GetState(quest) != required)
        {
            return false;
        }

        DataAssets? data = _services.GetService<DataAssets>();
        definition = data?.Find<QuestDefinition>(quest);
        if (definition is null)
        {
            LogMissingQuest(quest.Name);
            return false;
        }

        _facts.SetFact(quest, (int)state);
        Notify(quest);
        return true;
    }

    private void Notify(Tag quest)
    {
        QuestState state = GetState(quest);
        QuestChangedEvent changed = new(quest, state);
        _events.Send(changed);
    }

    /// <summary>Starts the quest's flow from its start, or resumes it at the <paramref name="active"/> nodes a save kept.</summary>
    private void StartFlow(QuestDefinition quest, IReadOnlyList<string>? active = null)
    {
        FlowGraphs? graphs = _services.GetService<FlowGraphs>();
        if (quest.Flow is not { Length: > 0 } path || graphs is null || _flows.ContainsKey(quest.Id))
        {
            return;
        }

        ConditionContext context = CreateContext();
        FlowGraph graph = graphs.Load(path);
        FlowRun run = new(graph, context);
        _flows.Add(quest.Id, run);
        run.Finished += _ => _flows.Remove(quest.Id);
        if (active is null)
        {
            run.Start();
        }
        else
        {
            run.Resume(active);
        }
    }

    private void StopFlow(Tag quest)
    {
        if (_flows.TryGetValue(quest, out FlowRun? run))
        {
            run.Finish();
        }
    }

    private ConditionContext CreateContext() => new(_facts) { Services = _services, Instigator = Player };

    private void HandleFactChanged(Tag tag, int oldValue, int newValue)
    {
        if (!_loading)
        {
            Evaluate();
        }
    }

    private SavedQuestFlows SaveFlows()
    {
        SavedQuestFlows saved = new();
        foreach ((Tag quest, FlowRun run) in _flows)
        {
            saved.Running[quest.Name] = [.. run.GetActiveIds()];
        }

        return saved;
    }

    private void RestoreFlows(SavedQuestFlows saved) => _savedFlows = saved.Running;

    /// <summary>Stops every flow and the quests' rules until the save is back, so nothing runs on the world being replaced.</summary>
    private void HandleLoading(SaveGame save)
    {
        _loading = true;
        _savedFlows = null;
        foreach (FlowRun run in _flows.Values.ToList())
        {
            run.Finish();
        }
    }

    /// <summary>
    /// Resumes the active quests' flows where the save kept them, leaving finished ones finished; a save that kept no flows (from
    /// before they were saved) starts them from their start.
    /// </summary>
    private void HandleLoaded(SaveGame save)
    {
        _loading = false;
        Evaluate();
        foreach (QuestDefinition quest in GetQuests(QuestState.Active))
        {
            if (_savedFlows is null)
            {
                StartFlow(quest);
            }
            else if (_savedFlows.TryGetValue(quest.Id.Name, out List<string>? active))
            {
                StartFlow(quest, active);
            }

            Notify(quest.Id);
        }

        _savedFlows = null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Quests still changed after {Passes} passes; check quests whose effects start or finish each other.")]
    private partial void LogStillChanging(int passes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No quest file has the id {Quest}.")]
    private partial void LogMissingQuest(string quest);
}
