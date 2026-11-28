namespace FlickerVoid.Editor.Core.Tags;

/// <summary>
/// The project's tags as a tree of dot-separated names: tags are picked from it, and added, renamed and removed in it. Adding a
/// tag adds its parents; renaming or removing one takes everything below it along.
/// </summary>
public sealed class TagCatalog
{
    private readonly SortedSet<string> _names = new(TagOrder.Instance);

    private IReadOnlyDictionary<string, int> _uses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    /// <summary>What rewrites the project's files when a tag is renamed or removed; none renames and removes in the catalog only.</summary>
    public ITagFiles? Files { get; set; }

    /// <summary>The tag a picker is handed when no project is open: set by the editor when a project opens.</summary>
    public static TagCatalog Current { get; set; } = new();

    public IReadOnlyCollection<string> Names => _names;

    public bool Contains(string name) => _names.Contains(name);

    /// <summary>How many times the project's files hold the tag itself.</summary>
    public int UsesOf(string name) => _uses.GetValueOrDefault(name);

    /// <summary>How many times the project's files hold the tag or a tag below it.</summary>
    public int UsesBelow(string name)
    {
        string prefix = $"{name}.";
        return _uses.Where(use => string.Equals(use.Key, name, StringComparison.OrdinalIgnoreCase) || use.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Sum(use => use.Value);
    }

    /// <summary>How many tags sit below the tag, at any depth.</summary>
    public int CountBelow(string name)
    {
        string prefix = $"{name}.";
        return _names.Count(other => other.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Takes the uses counted in the project's files; a tag in use joins the catalog.</summary>
    public void SetUses(IReadOnlyDictionary<string, int> uses)
    {
        Dictionary<string, int> counted = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, int count) in uses)
        {
            AddQuietly(name);
            counted[name] = counted.GetValueOrDefault(name) + count;
        }

        _uses = counted;
        Changed?.Invoke();
    }

    /// <summary>Replaces the tags without telling anyone: for loading the project's file.</summary>
    public void Load(IEnumerable<string> names)
    {
        _names.Clear();
        foreach (string name in names)
        {
            AddQuietly(name);
        }
    }

    public bool Add(string name)
    {
        if (!AddQuietly(name))
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Removes the tag and everything below it from the catalog and from the project's files; false, with why, when the files refuse.</summary>
    public bool Remove(string name, out string? why)
    {
        why = null;
        if (Files is { } files && UsesBelow(name) > 0 && !files.Remove(name, out why))
        {
            return false;
        }

        Remove(name);
        ForgetUses(name, null);
        return true;
    }

    public void Remove(string name)
    {
        string prefix = $"{name}.";
        int removed = _names.RemoveWhere(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase) || existing.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Renames the tag and everything below it in the catalog and in the project's files; false, with why, when it can't.</summary>
    public bool Rename(string name, string to, out string? why)
    {
        why = null;
        string target = to.Trim();
        if (!CanRename(name, target))
        {
            why = $"{target} is not a free tag name.";
            return false;
        }

        if (Files is { } files && UsesBelow(name) > 0 && !files.Rename(name, target, out why))
        {
            return false;
        }

        Rename(name, target);
        ForgetUses(name, target);
        return true;
    }

    private bool CanRename(string name, string target)
    {
        return IsValid(target) && _names.Contains(name) && (!_names.Contains(target) || string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Moves the counted uses of a tag and those below it to the new name, or drops them for none.</summary>
    private void ForgetUses(string name, string? to)
    {
        Dictionary<string, int> uses = new(StringComparer.OrdinalIgnoreCase);
        string prefix = $"{name}.";
        foreach ((string used, int count) in _uses)
        {
            bool below = string.Equals(used, name, StringComparison.OrdinalIgnoreCase) || used.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            if (!below)
            {
                uses[used] = uses.GetValueOrDefault(used) + count;
            }
            else if (to is not null)
            {
                string moved = to + used[name.Length..];
                uses[moved] = uses.GetValueOrDefault(moved) + count;
            }
        }

        _uses = uses;
        Changed?.Invoke();
    }

    /// <summary>Renames a tag and everything below it; empty when the new name is bad or already taken.</summary>
    public IReadOnlyList<(string Old, string New)> Rename(string name, string to)
    {
        string target = to.Trim();
        if (!CanRename(name, target))
        {
            return [];
        }

        string prefix = $"{name}.";
        List<string> moved = _names.Where(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase) || existing.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        List<(string Old, string New)> renamed = [];
        foreach (string old in moved)
        {
            _names.Remove(old);
            string renamedName = target + old[name.Length..];
            renamed.Add((old, renamedName));
        }

        foreach ((string _, string created) in renamed)
        {
            AddQuietly(created);
        }

        Changed?.Invoke();
        return renamed;
    }

    /// <summary>The tree flattened in order for a list: tags whose parent is folded are left out, and with a search only the matches and their parents are shown.</summary>
    public IReadOnlyList<TagRow> GetRows(string search, IReadOnlyCollection<string> folded)
    {
        List<TagRow> rows = [];
        string query = search.Trim();
        foreach (string path in _names)
        {
            if (query.Length > 0 && !Matches(path, query))
            {
                continue;
            }

            if (query.Length == 0 && IsHidden(path, folded))
            {
                continue;
            }

            int depth = path.Count(letter => letter == '.');
            string last = path[(path.LastIndexOf('.') + 1)..];
            string prefix = $"{path}.";
            bool hasChildren = _names.Any(other => other.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            rows.Add(new TagRow(path, last, depth, hasChildren, folded.Contains(path)));
        }

        return rows;
    }

    private bool Matches(string path, string query)
    {
        if (path.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = $"{path}.";
        return _names.Any(other => other.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && other.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHidden(string path, IReadOnlyCollection<string> folded)
    {
        int dot = path.LastIndexOf('.');
        while (dot > 0)
        {
            string parent = path[..dot];
            if (folded.Contains(parent))
            {
                return true;
            }

            dot = parent.LastIndexOf('.');
        }

        return false;
    }

    private bool AddQuietly(string name)
    {
        string trimmed = name.Trim();
        if (!IsValid(trimmed))
        {
            return false;
        }

        bool added = false;
        string[] parts = trimmed.Split('.');
        for (int count = 1; count <= parts.Length; count++)
        {
            added |= _names.Add(string.Join('.', parts.Take(count)));
        }

        return added;
    }

    private static bool IsValid(string name) => name.Length > 0 && name.Split('.').All(part => part.Length > 0);
}
