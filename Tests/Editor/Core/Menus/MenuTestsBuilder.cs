using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Core.Tests.Menus;

/// <summary>Builds menus of named choices in groups, recording which choices ran.</summary>
internal sealed class MenuTestsBuilder
{
    public List<string> Ran { get; } = [];

    public MenuChoice Choice(string name, string? group = null)
    {
        return new MenuChoice(name, () => Ran.Add(name), group);
    }

    /// <summary>Folder and Scene without a group, then Material and Mood in Look, then Flow Graph in Flow.</summary>
    public IReadOnlyList<MenuChoice> AssetChoices()
    {
        return [Choice("Folder"), Choice("Scene"), Choice("Material", "Look"), Choice("Mood", "Look"), Choice("Flow Graph", "Flow")];
    }
}
