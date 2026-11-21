using System.Text.Json.Nodes;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Editor.Assets.Flows;

/// <summary>
/// A flow graph file being edited: the graph editor edits <see cref="Graph"/> in place, and <see cref="Hold"/> keeps it unsaved
/// in the content until it is saved or discarded.
/// </summary>
public sealed class FlowDocument
{
    private readonly UnsavedContent _content;
    private readonly DataTypes _types;

    /// <summary>Reads the file; throws <see cref="InvalidDataException"/> when it can't be read.</summary>
    public FlowDocument(UnsavedContent content, DataTypes types, string path)
    {
        _content = content;
        _types = types;
        Path = path;

        // Unloading around the load reads the file's current contents, unsaved edits included, into objects nothing else holds.
        _content.Unload(path);
        JsonObject file = _content.Load(path, RuntimeJsonContext.Default.JsonObject);
        _content.Unload(path);
        Graph = FlowGraphs.Read(path, file, types);
    }

    public string Path { get; }

    public FlowGraph Graph { get; }

    /// <summary>Keeps the graph as the file's unsaved contents.</summary>
    public void Hold()
    {
        byte[] contents = FlowGraphs.Write(Graph, _types);
        _content.Edit(Path, contents);
    }
}
