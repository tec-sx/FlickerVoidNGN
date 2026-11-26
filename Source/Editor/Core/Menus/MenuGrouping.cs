namespace FlickerVoid.Editor.Core.Menus;

/// <summary>How a menu shows the groups of its choices; each menu picks its own.</summary>
public enum MenuGrouping
{
    /// <summary>One list, in the given order.</summary>
    None,

    /// <summary>Groups one after another, apart by separators, or by headings in a searchable menu.</summary>
    Separators,

    /// <summary>Groups under headings, kept while searching (the command palette).</summary>
    Sections,

    /// <summary>A submenu per group.</summary>
    Submenus,
}
