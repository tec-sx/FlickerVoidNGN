namespace FlickerVoid.Engine.Buildings;

/// <summary>What sets a building's camera trigger off: walking into a room, through an opening, onto stairs or onto an elevator.</summary>
public enum TriggerTarget
{
    Room,
    Opening,
    Stairs,
    Elevator,
}
