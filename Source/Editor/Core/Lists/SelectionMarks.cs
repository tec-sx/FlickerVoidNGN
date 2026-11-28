namespace FlickerVoid.Editor.Core.Lists;

/// <summary>Finds a menu item's mark from how many of the selected things have what it sets.</summary>
public static class SelectionMarks
{
    public static SelectionMark Of<T>(IEnumerable<T> selected, Func<T, bool> has)
    {
        int count = 0;
        int having = 0;
        foreach (T item in selected)
        {
            count++;
            if (has(item))
            {
                having++;
            }
        }

        if (having == 0)
        {
            return SelectionMark.None;
        }

        if (having == count)
        {
            return SelectionMark.All;
        }

        return SelectionMark.Some;
    }
}
