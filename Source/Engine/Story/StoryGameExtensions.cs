using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Engine.Story.Serialization;

namespace FlickerVoid.Engine.Story;

/// <summary>
/// Adds the story to a game: the <c>quest</c> and <c>knowledge</c> data asset types, the <c>questState</c> and <c>knows</c>
/// conditions, the <c>setQuestState</c> and <c>learn</c> effects, the quest flow nodes, and the quest and knowledge services.
/// </summary>
public static class StoryGameExtensions
{
    public static void AddStory(this Game game)
    {
        game.Data.AddResolver(StoryJsonContext.Default);
        game.Data.Register<QuestDefinition>("quest");
        game.Data.Register<KnowledgeDefinition>("knowledge");
        game.Data.RegisterDerived<Condition, QuestStateCondition>("questState");
        game.Data.RegisterDerived<Condition, KnowsCondition>("knows");
        game.Data.RegisterDerived<Effect, SetQuestStateEffect>("setQuestState");
        game.Data.RegisterDerived<Effect, LearnEffect>("learn");
        game.Data.RegisterDerived<FlowNode, SetQuestStateNode>("setQuestState");
        game.Data.RegisterDerived<FlowNode, CompleteObjectiveNode>("completeObjective");
        game.Data.RegisterDerived<FlowNode, WaitForQuestStateNode>("waitForQuestState");

        QuestSystem quests = new(game);
        game.FixedSystems.Add(quests);
        game.Services.AddService(quests);
        KnowledgeSystem knowledge = new(game.Facts, game.Services);
        game.Services.AddService(knowledge);
    }
}
