using System.Text;
using System.Text.RegularExpressions;

namespace FlickerVoid.Editor.Assets.Flows;

/// <summary>
/// Writes a new flow node class into a game project's code, like Unreal's Flow Node Blueprint: marked <c>[FlowNodeType]</c>,
/// which the project's generator registers, and added to the project's JSON context, which reads and writes its values.
/// </summary>
public static partial class FlowNodeSource
{
    private const string Suffix = "Node";

    /// <summary>The class a node's name becomes: its words in Pascal case, ending in Node ("Open Gate" is OpenGateNode).</summary>
    public static string GetTypeName(string name)
    {
        StringBuilder type = new();
        foreach (Match word in WordPattern().Matches(name))
        {
            type.Append(char.ToUpperInvariant(word.Value[0]));
            type.Append(word.Value[1..]);
        }

        if (!type.ToString().EndsWith(Suffix, StringComparison.Ordinal))
        {
            type.Append(Suffix);
        }

        return type.ToString();
    }

    /// <summary>The name files give the node in <c>$type</c>: the class without Node, in camel case (OpenGateNode is openGate).</summary>
    public static string GetTypeId(string typeName)
    {
        string id = typeName;
        if (id.Length > Suffix.Length && id.EndsWith(Suffix, StringComparison.Ordinal))
        {
            id = id[..^Suffix.Length];
        }

        return char.ToLowerInvariant(id[0]) + id[1..];
    }

    /// <summary>
    /// Writes the node class into the folder, in the namespace of its C# project and the folders below it, and registers it in
    /// the project's JSON context; returns the file. Throws <see cref="InvalidOperationException"/> saying why it can't.
    /// </summary>
    public static string Create(string folder, string name)
    {
        string typeName = GetTypeName(name);
        if (typeName == Suffix)
        {
            throw new InvalidOperationException($"'{name}' has no letters to name a class with.");
        }

        string file = Path.Combine(folder, typeName + ".cs");
        if (File.Exists(file))
        {
            throw new InvalidOperationException($"{file} already exists.");
        }

        string project = FindProject(folder)
            ?? throw new InvalidOperationException($"{folder} isn't in a C# project.");
        string projectFolder = Path.GetDirectoryName(project)!;
        string context = FindJsonContext(projectFolder)
            ?? throw new InvalidOperationException($"{project} has no JsonSerializerContext to read the node with.");
        string space = GetNamespace(project, folder);
        string typeId = GetTypeId(typeName);
        string text = $$"""
            using FlickerVoid.Engine.Flow.Graphs;

            namespace {{space}};

            /// <summary>{{name.Trim()}}: a flow node.</summary>
            [FlowNodeType("{{typeId}}")]
            public sealed class {{typeName}} : FlowNode<FlowOutput>
            {
                protected override void Execute(FlowContext<FlowOutput> context)
                {
                    context.Trigger(FlowOutput.Out);
                }
            }

            """;
        File.WriteAllText(file, text);
        Register(context, $"global::{space}.{typeName}");
        return file;
    }

    /// <summary>The nearest C# project in the folder or above it.</summary>
    private static string? FindProject(string folder)
    {
        DirectoryInfo? current = new(folder);
        while (current is not null)
        {
            FileInfo? project = current.EnumerateFiles("*.csproj").FirstOrDefault();
            if (project is not null)
            {
                return project.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>The project's name, then the folders from the project down to the folder.</summary>
    private static string GetNamespace(string project, string folder)
    {
        string space = Path.GetFileNameWithoutExtension(project);
        string projectFolder = Path.GetDirectoryName(project)!;
        string relative = Path.GetRelativePath(projectFolder, folder);
        if (relative == ".")
        {
            return space;
        }

        string below = relative.Replace(Path.DirectorySeparatorChar, '.');
        return $"{space}.{below}";
    }

    /// <summary>The file declaring the project's partial JsonSerializerContext, skipping build output.</summary>
    private static string? FindJsonContext(string projectFolder)
    {
        foreach (string file in Directory.EnumerateFiles(projectFolder, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(projectFolder, file);
            string first = relative.Split(Path.DirectorySeparatorChar)[0];
            if (first is "bin" or "obj")
            {
                continue;
            }

            string text = File.ReadAllText(file);
            if (ContextPattern().IsMatch(text))
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>Adds <c>[JsonSerializable(typeof(...))]</c> for the type just above the context's declaration.</summary>
    private static void Register(string context, string type)
    {
        string text = File.ReadAllText(context);
        Match declaration = ContextPattern().Match(text);
        int line = text.LastIndexOf('\n', declaration.Index) + 1;
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string attribute = $"[JsonSerializable(typeof({type}))]{newline}";
        string updated = text.Insert(line, attribute);
        File.WriteAllText(context, updated);
    }

    [GeneratedRegex("[A-Za-z][A-Za-z0-9]*")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"^[^\n]*partial class \w+\s*:\s*JsonSerializerContext", RegexOptions.Multiline)]
    private static partial Regex ContextPattern();
}
