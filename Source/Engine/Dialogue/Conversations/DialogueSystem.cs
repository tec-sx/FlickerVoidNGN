using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Dialogue.Barks;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>
/// Runs the one conversation at a time: starts a participant's dialogue graph, plays its lines and presents its choices through
/// events the interface handles, which answers with <see cref="Advance"/>, <see cref="Choose"/> and <see cref="End"/>. The
/// conversation ends when its graph finishes or has nothing left to do, when it is ended, or when its owner is gone. The game can't
/// be saved during a conversation (<see cref="SaveBlock"/>), and a load or leaving the scene ends it.
/// </summary>
public sealed partial class DialogueSystem : BaseSystem<World, GameTime>
{
    /// <summary>Held on the saves while a conversation runs, as its graph's place isn't saved.</summary>
    public static readonly Tag SaveBlock = new("Save.Block.Dialogue");

    private static readonly QueryDescription Participants = new QueryDescription().WithAll<DialogueParticipant>();

    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly EventBus _events;
    private readonly DialogueSettings _settings;
    private readonly ILogger<DialogueSystem> _logger;
    private readonly List<Wait> _waits = [];
    private SaveSystem? _saves;
    private SceneTravel? _travel;
    private readonly Dictionary<Entity, string> _lastBarks = [];
    private FlowRun? _run;
    private FlowRun? _pendingRun;
    private FlowNode? _pendingNode;
    private Action? _lineFinished;
    private Action<int>? _choiceMade;
    private float _lineLeft;

    public DialogueSystem(Game game, DialogueSettings settings, Random random)
        : base(game.World)
    {
        _facts = game.Facts;
        _services = game.Services;
        _events = game.Events;
        _settings = settings;
        Random = random;
        _logger = game.LoggerFactory.CreateLogger<DialogueSystem>();
    }

    public bool IsInConversation { get; private set; }

    /// <summary>Who started the conversation, usually the player.</summary>
    public Entity Instigator { get; private set; } = Entity.Null;

    /// <summary>Whose dialogue it is, usually the character talked to.</summary>
    public Entity Owner { get; private set; } = Entity.Null;

    public DialogueLine CurrentLine { get; private set; } = DialogueLine.None;

    public IReadOnlyList<DialogueChoice> Choices { get; private set; } = [];

    public bool IsWaitingForChoice => _choiceMade is not null;

    /// <summary>Picks among fitting barks.</summary>
    internal Random Random { get; }

    /// <summary>Starts the owner's dialogue graph with the instigator; false while another conversation runs or when the owner has none.</summary>
    public bool Start(Entity instigator, Entity owner)
    {
        FlowGraphs? graphs = _services.GetService<FlowGraphs>();
        if (IsInConversation || graphs is null || !World.IsAlive(owner) || !World.TryGet(owner, out DialogueParticipant participant)
            || participant.Dialogue.Length == 0)
        {
            LogCantStart(owner.Id);
            return false;
        }

        FlowGraph graph;
        try
        {
            graph = graphs.Load(participant.Dialogue);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogUnreadable(participant.Dialogue, exception.Message);
            return false;
        }

        WorldEntityView instigatorView = new(World, instigator);
        WorldEntityView ownerView = new(World, owner);
        ConditionContext context = new(_facts) { Services = _services, Instigator = instigatorView, Target = ownerView };
        FlowRun run = new(graph, context);
        Begin(instigator, owner, run);
        run.Start();
        CloseIfIdle();
        return true;
    }

    /// <summary>Ends the conversation and stops its graph.</summary>
    public void End() => Close(stopGraph: true);

    /// <summary>Finishes the current line when it can be skipped or waits for input.</summary>
    public void Advance()
    {
        if (_lineFinished is not null && (CurrentLine.Skippable || CurrentLine.WaitForInput))
        {
            FinishLine();
        }
    }

    /// <summary>Picks the presented choice with this index; false when there is none or it is locked.</summary>
    public bool Choose(int index)
    {
        DialogueChoice? choice = null;
        foreach (DialogueChoice presented in Choices)
        {
            if (presented.Index == index)
            {
                choice = presented;
            }
        }

        if (choice is not { Available: true } || _choiceMade is not { } chosen)
        {
            return false;
        }

        ClearPending();
        Choices = [];
        chosen(index);
        CloseIfIdle();
        return true;
    }

    public override void Update(in GameTime time)
    {
        if (IsInConversation && Owner != Entity.Null && !World.IsAlive(Owner))
        {
            LogOwnerGone();
            Close(stopGraph: true);
        }

        float elapsed = (float)time.Elapsed.TotalSeconds;
        if (_lineFinished is not null && !CurrentLine.WaitForInput)
        {
            _lineLeft -= elapsed;
            if (_lineLeft <= 0)
            {
                FinishLine();
            }
        }

        for (int index = _waits.Count - 1; index >= 0; index--)
        {
            Wait wait = _waits[index];
            wait.Left -= elapsed;
            if (wait.Left <= 0)
            {
                _waits.RemoveAt(index);
                wait.Done();
            }
        }

        CloseIfIdle();
    }

    /// <summary>Follows loads and travel, which end the conversation, as the game a load brings back has none and travel takes its owner away.</summary>
    public override void Initialize()
    {
        _saves = _services.GetService<SaveSystem>();
        if (_saves is not null)
        {
            _saves.Loading += HandleLoading;
        }

        _travel = _services.GetService<SceneTravel>();
        if (_travel is not null)
        {
            _travel.Leaving += HandleLeaving;
        }
    }

    public override void Dispose()
    {
        if (_saves is not null)
        {
            _saves.Loading -= HandleLoading;
        }

        if (_travel is not null)
        {
            _travel.Leaving -= HandleLeaving;
        }

        Close(stopGraph: true);
        base.Dispose();
    }

    /// <summary>Opens a conversation for a graph that started without <see cref="Start"/>, between its run's instigator and target.</summary>
    internal void EnsureConversation(FlowRun run)
    {
        if (IsInConversation)
        {
            return;
        }

        Entity instigator = GetEntity(run.Conditions.Instigator);
        Entity owner = GetEntity(run.Conditions.Target);
        LogImplicitConversation();
        Begin(instigator, owner, run);
    }

    internal void PlayLine(DialogueLine line, FlowRun run, FlowNode node, Action finished)
    {
        ClearPending();
        Entity speaker = ResolveSpeaker(line.Speaker);
        float duration = line.Duration;
        if (duration <= 0)
        {
            duration = MathF.Max(_settings.MinLineDuration, line.Text.Length * _settings.SecondsPerCharacter);
        }

        CurrentLine = line with { SpeakerEntity = speaker, Duration = duration };
        _lineLeft = duration;
        _pendingRun = run;
        _pendingNode = node;
        _lineFinished = finished;
        LineStartedEvent started = new(CurrentLine);
        _events.Send(started);
    }

    internal void PresentChoices(IReadOnlyList<DialogueChoice> choices, FlowRun run, FlowNode node, Action<int> chosen)
    {
        ClearPending();
        Choices = choices;
        _pendingRun = run;
        _pendingNode = node;
        _choiceMade = chosen;
        ChoicesPresentedEvent presented = new(choices);
        _events.Send(presented);
    }

    /// <summary>Drops the line or choice the node asked for, as the node stops being active.</summary>
    internal void Cancel(FlowRun run, FlowNode node)
    {
        if (_pendingRun == run && _pendingNode == node)
        {
            ClearPending();
        }

        _waits.RemoveAll(wait => wait.Run == run && wait.Node == node);
    }

    /// <summary>Calls <paramref name="done"/> after <paramref name="seconds"/> of game time, unless the node is cancelled first.</summary>
    internal void WaitFor(float seconds, FlowRun run, FlowNode node, Action done)
    {
        Wait wait = new(run, node, done) { Left = seconds };
        _waits.Add(wait);
    }

    internal void Bark(Entity speaker, string row, string text, float duration)
    {
        _lastBarks[speaker] = row;
        BarkEvent bark = new(speaker, text, duration);
        _events.Send(bark);
    }

    internal string? GetLastBark(Entity speaker) => _lastBarks.GetValueOrDefault(speaker);

    /// <summary>The entity that speaks for a character id: the owner for none, else the instigator or owner when it matches, else any participant.</summary>
    internal Entity ResolveSpeaker(Tag speaker)
    {
        if (!speaker.IsValid)
        {
            return Owner;
        }

        if (IsSpeaker(Instigator, speaker))
        {
            return Instigator;
        }

        if (IsSpeaker(Owner, speaker))
        {
            return Owner;
        }

        Entity found = Entity.Null;
        World.Query(in Participants, (Entity entity, ref DialogueParticipant participant) =>
        {
            if (found == Entity.Null && participant.Speaker == speaker)
            {
                found = entity;
            }
        });
        return found;
    }

    private static Entity GetEntity(IEntityView? view)
    {
        if (view is WorldEntityView worldView)
        {
            return worldView.Entity;
        }

        return Entity.Null;
    }

    private bool IsSpeaker(Entity entity, Tag speaker)
    {
        return entity != Entity.Null && World.IsAlive(entity) && World.TryGet(entity, out DialogueParticipant participant) && participant.Speaker == speaker;
    }

    private void Begin(Entity instigator, Entity owner, FlowRun run)
    {
        IsInConversation = true;
        Instigator = instigator;
        Owner = owner;
        _run = run;
        run.Finished += HandleRunFinished;
        SaveSystem? saves = _services.GetService<SaveSystem>();
        saves?.AddBlock(SaveBlock);
        ConversationStartedEvent started = new(instigator, owner);
        _events.Send(started);
    }

    private void FinishLine()
    {
        DialogueLine finished = CurrentLine;
        Action? callback = _lineFinished;
        ClearPending();
        LineFinishedEvent sent = new(finished);
        _events.Send(sent);
        callback?.Invoke();
        CloseIfIdle();
    }

    private void ClearPending()
    {
        _pendingRun = null;
        _pendingNode = null;
        _lineFinished = null;
        _choiceMade = null;
    }

    /// <summary>Ends a conversation whose graph has nothing left to do: no active node waits for a line, a choice or time.</summary>
    private void CloseIfIdle()
    {
        if (_run is { IsRunning: true, ActiveNodes.Count: 0 })
        {
            Close(stopGraph: true);
        }
    }

    private void Close(bool stopGraph)
    {
        if (!IsInConversation)
        {
            return;
        }

        FlowRun? run = _run;
        Entity instigator = Instigator;
        Entity owner = Owner;
        ClearPending();
        IsInConversation = false;
        Instigator = Entity.Null;
        Owner = Entity.Null;
        CurrentLine = DialogueLine.None;
        Choices = [];
        _run = null;
        if (run is not null)
        {
            run.Finished -= HandleRunFinished;
            if (stopGraph)
            {
                run.Finish();
            }
        }

        SaveSystem? saves = _services.GetService<SaveSystem>();
        saves?.RemoveBlock(SaveBlock);
        ConversationEndedEvent ended = new(instigator, owner);
        _events.Send(ended);
    }

    private void HandleRunFinished(FlowRun run) => Close(stopGraph: false);

    private void HandleLoading(SaveGame save) => End();

    private void HandleLeaving(string scene) => End();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Can't start a conversation with entity {Owner}: one is running, or it has no dialogue participant with a dialogue.")]
    private partial void LogCantStart(int owner);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The dialogue {Path} can't be read: {Reason}")]
    private partial void LogUnreadable(string path, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Closing the conversation: its owner is gone.")]
    private partial void LogOwnerGone();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A dialogue node opened a conversation outside DialogueSystem.Start; it ends with its graph.")]
    private partial void LogImplicitConversation();

    /// <summary>A node waiting for game time to pass.</summary>
    private sealed class Wait(FlowRun run, FlowNode node, Action done)
    {
        public FlowRun Run { get; } = run;

        public FlowNode Node { get; } = node;

        public Action Done { get; } = done;

        public float Left { get; set; }
    }
}
