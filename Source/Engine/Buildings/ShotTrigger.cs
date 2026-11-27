namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// What switches the game to a building's shot: walking into a room (by its name), through an opening, onto stairs or onto an
/// elevator (by its id) on a storey, when, how and for how long, and where it goes back to.
/// </summary>
public sealed class ShotTrigger
{
    public TriggerTarget Target { get; set; }

    /// <summary>The level of the storey the room or opening is on.</summary>
    public int Level { get; set; }

    /// <summary>The room's name.</summary>
    public string Room { get; set; } = string.Empty;

    /// <summary>The opening's, stairs' or elevator's id.</summary>
    public int Id { get; set; }

    public TriggerMoment When { get; set; } = TriggerMoment.Enter;

    public TriggerChange Change { get; set; } = TriggerChange.Blend;

    /// <summary>Seconds a blend or a fade takes.</summary>
    public float Time { get; set; } = 0.6f;

    public TriggerReturn Return { get; set; }
}
