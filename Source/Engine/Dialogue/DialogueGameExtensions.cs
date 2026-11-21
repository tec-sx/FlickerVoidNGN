using FlickerVoid.Engine.Dialogue.Barks;
using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Dialogue.Serialization;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Dialogue;

/// <summary>
/// Adds dialogue to a game: the <c>dialogueParticipant</c> component, the <c>line</c>, <c>choice</c> and <c>bark</c> flow nodes,
/// the <c>bark</c> table row, the <c>startConversation</c> effect and the system that runs conversations.
/// </summary>
public static class DialogueGameExtensions
{
    public static DialogueSystem AddDialogue(this Game game, DialogueSettings? settings = null, Random? random = null)
    {
        game.Data.AddResolver(DialogueJsonContext.Default);
        game.Data.RegisterDerived<FlowNode, LineNode>("line");
        game.Data.RegisterDerived<FlowNode, ChoiceNode>("choice");
        game.Data.RegisterDerived<FlowNode, BarkNode>("bark");
        game.Data.RegisterRow<BarkRow>("bark");
        game.Data.RegisterDerived<Effect, StartConversationEffect>("startConversation");
        game.Spawner.Register("dialogueParticipant", DialogueJsonContext.Default.DialogueParticipant);

        DialogueSystem dialogue = new(game, settings ?? new DialogueSettings(), random ?? Random.Shared);
        game.FixedSystems.Add(dialogue);
        game.Services.AddService(dialogue);
        return dialogue;
    }
}
