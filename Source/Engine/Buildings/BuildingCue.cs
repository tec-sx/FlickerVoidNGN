namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// A building's camera trigger, on a part with a trigger area: the id of the shot part it switches to in the same building, when, how
/// and how fast, and where it goes back to on leaving.
/// </summary>
public struct BuildingCue
{
    public string Shot { get; set; }

    public TriggerMoment When { get; set; }

    public TriggerChange Change { get; set; }

    public float Time { get; set; }

    public TriggerReturn Return { get; set; }
}
