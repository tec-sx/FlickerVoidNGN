namespace FlickerVoid.Editor.Assets.Buildings.Rows;

/// <summary>A faint line of help under a tool's sections.</summary>
public sealed class NoteRow(string text) : ToolRow(text)
{
    public override void Take(ToolRow other)
    {
    }
}
