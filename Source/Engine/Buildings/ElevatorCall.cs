namespace FlickerVoid.Engine.Buildings;

/// <summary>A call button beside an elevator's shaft: the id of the platform it calls, a part of the same building, and the stop it calls it to.</summary>
public struct ElevatorCall
{
    public string Platform { get; set; }

    public int Stop { get; set; }
}
