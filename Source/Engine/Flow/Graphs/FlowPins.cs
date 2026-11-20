namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>The pins an enum declares, in declared order: their names, as links name them, and their values.</summary>
internal static class FlowPins<T>
    where T : struct, Enum
{
    public static readonly T[] Values = Enum.GetValues<T>();

    public static readonly string[] Names = Enum.GetNames<T>();

    public static int IndexOf(T pin) => Array.IndexOf(Values, pin);
}
