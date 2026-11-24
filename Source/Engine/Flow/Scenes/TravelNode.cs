using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Flow.Scenes;

/// <summary>Travels to a scene before the next tick, standing the travellers at its entry, then passes the signal on once there.</summary>
public sealed class TravelNode : FlowNode<FlowOutput>
{
    [ContentFile(".fvscene")]
    public string Scene { get; set; } = string.Empty;

    /// <summary>The name of the next scene's entity the travellers stand at; empty leaves them where that scene places them.</summary>
    public string Entry { get; set; } = string.Empty;

    public override string Describe() => Scene;

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        SceneTravel? travel = context.Conditions.GetService<SceneTravel>();
        if (travel is null || Scene.Length == 0)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        string? entry = null;
        if (Entry.Length > 0)
        {
            entry = Entry;
        }

        context.StayActive();
        travel.Request(Scene, entry, () => context.Trigger(FlowOutput.Out));
    }
}
