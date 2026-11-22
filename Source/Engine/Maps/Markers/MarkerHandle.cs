namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>Names a marker of the map system.</summary>
public readonly record struct MarkerHandle(int Id)
{
    public static MarkerHandle None => new(-1);

    public bool IsValid => Id >= 0;
}
