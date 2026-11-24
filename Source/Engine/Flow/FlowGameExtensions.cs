using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Flow.Nodes;
using FlickerVoid.Engine.Flow.Scenes;
using FlickerVoid.Engine.Flow.Serialization;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Flow;

/// <summary>Adds flow graphs to a game: the core nodes and the <c>travel</c> node and effect as data types, and the <c>flow</c> schema.</summary>
public static class FlowGameExtensions
{
    public static void AddFlow(this Game game)
    {
        game.Data.AddResolver(FlowJsonContext.Default);
        game.Data.RegisterDerived<FlowNode, StartNode>("start");
        game.Data.RegisterDerived<FlowNode, FinishNode>("finish");
        game.Data.RegisterDerived<FlowNode, BranchNode>("branch");
        game.Data.RegisterDerived<FlowNode, ApplyEffectsNode>("applyEffects");
        game.Data.RegisterDerived<FlowNode, WaitForConditionsNode>("waitForConditions");
        game.Data.RegisterDerived<FlowNode, TravelNode>("travel");
        game.Data.RegisterDerived<Effect, TravelEffect>("travel");
        game.Schemas.Add("flow", () => FlowGraphs.GetSchema(game.Data));
    }
}
