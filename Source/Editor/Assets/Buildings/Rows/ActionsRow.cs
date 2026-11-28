namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>Buttons side by side, such as On the floor and Centre on wall.</summary>
public sealed class ActionsRow(string label, IReadOnlyList<ToolAction> actions) : ToolRow(label)
{
    public IReadOnlyList<ToolAction> Actions { get; private set; } = actions;

    public override void Take(ToolRow other)
    {
        Actions = ((ActionsRow)other).Actions;
        OnPropertyChanged(nameof(Actions));
    }
}
