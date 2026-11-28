using FlickerVoid.Editor.Core.Lists;

namespace FlickerVoid.Editor.Core.Tests.Lists;

/// <summary>Builds a selection over the shown rows "a" to "f", with nothing selected.</summary>
internal sealed class ListSelectionTestsBuilder
{
    public static readonly string[] Rows = ["a", "b", "c", "d", "e", "f"];

    public ListSelection<string> Build()
    {
        ListSelection<string> selection = new();
        selection.Show(Rows);
        return selection;
    }
}
