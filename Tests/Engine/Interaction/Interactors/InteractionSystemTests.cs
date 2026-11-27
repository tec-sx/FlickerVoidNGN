using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Platform.Input;

namespace FlickerVoid.Engine.Interaction.Tests.Interactors;

public sealed class InteractionSystemTests : IDisposable
{
    private const string Lever = """
        {
          "type": "interactable",
          "id": "Interactable.Lever",
          "offers": [
            {
              "input": "Input.Use",
              "action": "Interaction.Action.Pull",
              "display": { "name": { "key": "Test.Pull", "text": "Pull" } },
              "effects": { "effects": [ { "$type": "fact", "fact": "World.Lever.Pulled", "value": 1 } ] }
            }
          ]
        }
        """;

    private const string Hero = """{ "name": "Hero", "components": { "transform": {}, "interactor": { "reach": 2 } } }""";

    private static readonly Tag Use = new("Input.Use");
    private static readonly Tag Pull = new("Interaction.Action.Pull");

    private readonly InteractionSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void PressingTheOfferedInputDoesItsActionAndAppliesItsEffects()
    {
        _builder.WithFile("Lever.fvdata", Lever).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(1);
        List<InteractionActionEvent> actions = _builder.Collect<InteractionActionEvent>();
        List<InteractionEndedEvent> ended = _builder.Collect<InteractionEndedEvent>();

        _builder.Send(new FocusCommand(hero, lever));
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));

        actions.Should().ContainSingle().Which.Should().Be(new InteractionActionEvent(hero, lever, Pull));
        ended.Should().ContainSingle().Which.End.Should().Be(InteractionEnd.Succeeded);
        _builder.Game.Facts.GetFact(new Tag("World.Lever.Pulled")).Should().Be(1);
    }

    [Fact]
    public void ListsTheFocusedOffersHeaviestFirstWithTheReasonsLockedOnesShow()
    {
        _builder.WithFile("Chest.fvdata", """
            {
              "type": "interactable",
              "offers": [
                { "input": "Input.Look", "action": "Interaction.Action.Look", "weight": 1 },
                { "input": "Input.Use", "action": "Interaction.Action.Open", "weight": 5,
                  "conditions": { "conditions": [ { "$type": "fact", "fact": "Has.Key" } ],
                                  "failurePresentation": "ShowLockedWithReason", "failureReason": { "key": "Test.NeedsAKey", "text": "Needs a key" } } },
                { "input": "Input.Steal", "action": "Interaction.Action.Steal",
                  "conditions": { "conditions": [ { "$type": "fact", "fact": "Is.Thief" } ] } }
              ]
            }
            """).Build();
        IReadOnlyList<Entity> spawned = _builder.Spawn(Hero + """, { "name": "Chest", "components": { "transform": {}, "interactable": { "definition": "Chest.fvdata" } } }""");
        _builder.Send(new FocusCommand(spawned[0], spawned[1]));

        IReadOnlyList<OfferView> offers = _builder.Game.Services.GetService<InteractionSystem>()!.GetOffers(spawned[0]);

        offers.Select(offer => offer.Input.Name).Should().Equal("Input.Use", "Input.Look");
        offers[0].Available.Should().BeFalse();
        offers[0].LockedReason.Text.Should().Be("Needs a key");
        offers[1].CanExecute.Should().BeTrue();
    }

    [Fact]
    public void ListsTheOffersOfAnInteractableItIsNotFocusedOn()
    {
        _builder.WithFile("Lever.fvdata", Lever).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(1);

        IReadOnlyList<OfferView> offers = _builder.Game.Services.GetService<InteractionSystem>()!.GetOffers(hero, lever);
        IReadOnlyList<OfferView> focused = _builder.Game.Services.GetService<InteractionSystem>()!.GetOffers(hero);

        offers.Should().ContainSingle();
        focused.Should().BeEmpty();
    }

    [Fact]
    public void DoesNothingOutOfReach()
    {
        _builder.WithFile("Lever.fvdata", Lever).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(5);
        List<InteractionStartedEvent> started = _builder.Collect<InteractionStartedEvent>();

        _builder.Send(new FocusCommand(hero, lever));
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));

        started.Should().BeEmpty();
    }

    [Fact]
    public void AHoldCompletesAfterItsDurationAndFailsWhenReleasedEarly()
    {
        _builder.WithFile("Lever.fvdata", Lever.Replace("\"display\"", "\"gesture\": { \"mode\": \"Hold\", \"duration\": 1 }, \"display\"")).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(1);
        List<InteractionEndedEvent> ended = _builder.Collect<InteractionEndedEvent>();
        _builder.Send(new FocusCommand(hero, lever));

        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));
        _builder.Run(0.5f);
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Released));
        _builder.Run(0.1f);
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));
        _builder.Run(1.1f);

        ended.Select(end => end.End).Should().Equal(InteractionEnd.Released, InteractionEnd.Succeeded);
    }

    [Fact]
    public void LosingFocusCancelsWhatIsUnderWay()
    {
        _builder.WithFile("Lever.fvdata", Lever.Replace("\"display\"", "\"gesture\": { \"mode\": \"Hold\", \"duration\": 1 }, \"display\"")).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(1);
        List<InteractionEndedEvent> ended = _builder.Collect<InteractionEndedEvent>();
        _builder.Send(new FocusCommand(hero, lever));
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));

        _builder.Send(new FocusCommand(hero, Entity.Null));
        _builder.Run(0.1f);

        ended.Should().ContainSingle().Which.End.Should().Be(InteractionEnd.FocusLost);
        _builder.Game.World.Get<ActiveInteractable>(lever).State.Should().Be(InteractableState.Awake);
    }

    [Fact]
    public void CompletesOnceEveryOfferIsUsedUp()
    {
        _builder.WithFile("Lever.fvdata", Lever.Replace("\"display\"", "\"uses\": 1, \"display\"")).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(1);
        List<InteractionActionEvent> actions = _builder.Collect<InteractionActionEvent>();
        _builder.Send(new FocusCommand(hero, lever));

        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));

        actions.Should().ContainSingle();
        _builder.Game.World.Get<ActiveInteractable>(lever).State.Should().Be(InteractableState.Completed);
    }

    [Fact]
    public void CoolsDownBetweenUses()
    {
        _builder.WithFile("Lever.fvdata", Lever.Replace("\"offers\"", "\"cooldown\": 1, \"offers\"")).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(1);
        List<InteractionActionEvent> actions = _builder.Collect<InteractionActionEvent>();
        _builder.Send(new FocusCommand(hero, lever));

        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));
        _builder.Run(1.1f);
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));

        actions.Should().HaveCount(2);
    }

    [Fact]
    public void OnlyCompatibleInteractorsUseIt()
    {
        _builder.WithFile("Lever.fvdata", Lever.Replace("\"offers\"", "\"compatibleInteractors\": [ \"Interactor.Player\" ], \"offers\"")).Build();
        IReadOnlyList<Entity> spawned = _builder.Spawn("""
            { "name": "Cat", "components": { "transform": {}, "interactor": { "kind": "Interactor.Animal" } } },
            { "name": "Hero", "components": { "transform": {}, "interactor": { "kind": "Interactor.Player" } } },
            { "name": "Lever", "components": { "transform": { "position": [1, 0, 0] }, "interactable": { "definition": "Lever.fvdata" } } }
            """);
        List<InteractionActionEvent> actions = _builder.Collect<InteractionActionEvent>();

        foreach (Entity interactor in spawned.Take(2))
        {
            _builder.Send(new FocusCommand(interactor, spawned[2]));
            _builder.Send(new InteractionInput(interactor, Use, InputPhase.Pressed));
        }

        actions.Should().ContainSingle().Which.Interactor.Should().Be(spawned[1]);
    }

    [Fact]
    public void APressDoesTheHeaviestOfferForItsInputThatCanBeDone()
    {
        _builder.WithFile("Lever.fvdata", """
            {
              "type": "interactable",
              "offers": [
                { "input": "Input.Use", "action": "Interaction.Action.Pull" },
                { "input": "Input.Use", "action": "Interaction.Action.Oil", "weight": 2,
                  "conditions": { "conditions": [ { "$type": "fact", "fact": "Lever.Rusty" } ] } }
              ]
            }
            """).Build();
        (Entity hero, Entity lever) = SpawnHeroAndLever(1);
        List<InteractionActionEvent> actions = _builder.Collect<InteractionActionEvent>();
        _builder.Send(new FocusCommand(hero, lever));

        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));
        _builder.Game.Facts.SetFact(new Tag("Lever.Rusty"), 1);
        _builder.Send(new InteractionInput(hero, Use, InputPhase.Pressed));

        actions.Select(action => action.Action.Name).Should().Equal("Interaction.Action.Pull", "Interaction.Action.Oil");
    }

    private (Entity Hero, Entity Lever) SpawnHeroAndLever(float distance)
    {
        string lever = $$"""{ "name": "Lever", "components": { "transform": { "position": [{{distance}}, 0, 0] }, "interactable": { "definition": "Lever.fvdata" } } }""";
        IReadOnlyList<Entity> spawned = _builder.Spawn(Hero + ", " + lever);
        return (spawned[0], spawned[1]);
    }
}
