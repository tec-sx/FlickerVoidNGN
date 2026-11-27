namespace FlickerVoid.Editor.Core.Tags;

/// <summary>Orders tags as a tree, part by part ignoring case, so a tag's children follow it before any tag that only shares its start.</summary>
public sealed class TagOrder : IComparer<string>
{
    public static TagOrder Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        string[] left = (x ?? "").Split('.');
        string[] right = (y ?? "").Split('.');
        int count = Math.Min(left.Length, right.Length);
        for (int index = 0; index < count; index++)
        {
            int order = StringComparer.OrdinalIgnoreCase.Compare(left[index], right[index]);
            if (order != 0)
            {
                return order;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
