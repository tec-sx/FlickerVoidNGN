namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>A row of options side by side, one chosen; choosing hands its index to the row's action.</summary>
public sealed class ChoiceRow : ToolRow
{
    private Action<int> _set;

    public ChoiceRow(string label, IReadOnlyList<string> options, int chosen, Action<int> set)
        : base(label)
    {
        _set = set;
        List<ChoiceOption> made = [];
        for (int index = 0; index < options.Count; index++)
        {
            int picked = index;
            ChoiceOption option = new(options[index], () => Choose(picked)) { IsChosen = index == chosen };
            made.Add(option);
        }

        Options = made;
    }

    public IReadOnlyList<ChoiceOption> Options { get; }

    public override bool Matches(ToolRow other) => base.Matches(other) && ((ChoiceRow)other).Options.Count == Options.Count;

    public override void Take(ToolRow other)
    {
        ChoiceRow row = (ChoiceRow)other;
        _set = row._set;
        for (int index = 0; index < Options.Count; index++)
        {
            Options[index].IsChosen = row.Options[index].IsChosen;
        }
    }

    private void Choose(int index)
    {
        for (int option = 0; option < Options.Count; option++)
        {
            Options[option].IsChosen = option == index;
        }

        _set(index);
    }
}
