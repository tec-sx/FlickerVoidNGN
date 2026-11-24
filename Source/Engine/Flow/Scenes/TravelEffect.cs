using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Flow.Scenes;

/// <summary>Travels to a scene before the next tick, standing the travellers at its entry, such as stairs leading down to a cellar.</summary>
public sealed class TravelEffect : Effect
{
    [ContentFile(".fvscene")]
    public string Scene { get; set; } = string.Empty;

    /// <summary>The name of the next scene's entity the travellers stand at; empty leaves them where that scene places them.</summary>
    public string Entry { get; set; } = string.Empty;

    public override void Apply(ConditionContext context)
    {
        SceneTravel? travel = context.GetService<SceneTravel>();
        if (travel is null || Scene.Length == 0)
        {
            return;
        }

        string? entry = null;
        if (Entry.Length > 0)
        {
            entry = Entry;
        }

        travel.Request(Scene, entry);
    }

    public override string Describe() => $"Travel to {Scene}";
}
