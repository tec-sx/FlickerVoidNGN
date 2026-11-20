namespace FlickerVoid.Engine.Editor;

/// <summary>What the editor opens and how, from its command line.</summary>
public sealed record EditorOptions
{
    public const string Usage = "Usage: FlickerVoid.Engine.Editor [<project.fvproject> | --content <built content folder>] [--scene <scene file in the content>] [--validation]";

    /// <summary>A built content folder, which the scene and the files it names are in; null opens an empty world.</summary>
    public string? Content { get; init; }

    /// <summary>A project file to build and open, in place of a content folder.</summary>
    public string? Project { get; init; }

    /// <summary>The scene to open, relative to the content folder.</summary>
    public string? Scene { get; init; }

    /// <summary>The project's built game, which Play runs; null when no project is open.</summary>
    public string? Game { get; init; }

    public bool Validation { get; init; }

    /// <summary>Reads the command line, or returns null and says what is wrong with it.</summary>
    public static EditorOptions? Parse(IReadOnlyList<string> args, out string? error)
    {
        EditorOptions options = new();
        error = null;
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            bool hasValue = index + 1 < args.Count;
            switch (arg)
            {
                case "--validation":
                    options = options with { Validation = true };
                    break;
                case "--content" when hasValue:
                    index++;
                    string content = Path.GetFullPath(args[index]);
                    options = options with { Content = content };
                    break;
                case "--scene" when hasValue:
                    index++;
                    options = options with { Scene = args[index] };
                    break;
                case { } when arg.EndsWith(".fvproject", StringComparison.OrdinalIgnoreCase):
                    string project = Path.GetFullPath(arg);
                    options = options with { Project = project };
                    break;
                default:
                    error = $"Unknown or incomplete option {arg}. {Usage}";
                    return null;
            }
        }

        if (options.Scene is not null && options.Content is null && options.Project is null)
        {
            error = $"--scene needs a project or --content. {Usage}";
            return null;
        }

        if (options.Project is not null && options.Content is not null)
        {
            error = $"Open a project or a content folder, not both. {Usage}";
            return null;
        }

        return options;
    }
}
