using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Assets.Flows;

/// <summary>
/// A flow graph file being edited: the graph editor edits <see cref="Graph"/> in place, and <see cref="ContentDocument{T}.Hold"/> keeps
/// it unsaved in the content until it is saved or discarded. Throws <see cref="InvalidDataException"/> when the file can't be read.
/// </summary>
public sealed class FlowDocument(UnsavedContent content, DataTypes types, string path)
    : ContentDocument<FlowGraph>(content, path, file => FlowGraphs.Read(path, file, types), (graph, _) => FlowGraphs.Write(graph, types))
{
    public FlowGraph Graph => Value;
}
