namespace FlickerVoid.Engine.Runtime.Solids;

/// <summary>An edge of a solid by its two points' numbers, the smaller first, so it is the same edge whichever way it is named.</summary>
public readonly record struct SolidEdge(int A, int B)
{
    public static SolidEdge Of(int first, int second)
    {
        if (first < second)
        {
            return new SolidEdge(first, second);
        }

        return new SolidEdge(second, first);
    }

    /// <summary>The point at the other end from <paramref name="point"/>.</summary>
    public int GetOther(int point)
    {
        if (point == A)
        {
            return B;
        }

        return A;
    }
}
