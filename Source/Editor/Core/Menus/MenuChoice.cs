namespace FlickerVoid.Editor.Core.Menus;

/// <summary>
/// One thing a menu offers: its name, the group it belongs to (menus decide whether and how to show groups, see
/// <see cref="MenuGrouping"/>), an icon named as in the shell's icon set, the key that also does it and what it does.
/// </summary>
public sealed record MenuChoice(string Name, Action Run, string? Group = null, string? Icon = null, string? Gesture = null)
{
    /// <summary>Whether the search text is part of the choice's name or group, ignoring case.</summary>
    public bool Matches(string search)
    {
        if (Name.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Group is not null && Group.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
