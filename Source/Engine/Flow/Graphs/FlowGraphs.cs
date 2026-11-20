using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>Reads flow graph files with the node types registered in <see cref="DataTypes"/> and starts runs of them.</summary>
public sealed class FlowGraphs(IContentManager content, DataTypes types)
{
    /// <summary>The graph at <paramref name="path"/>, read fresh, so edits on disk reach the next run; throws <see cref="InvalidDataException"/> when it can't be read.</summary>
    public FlowGraph Load(string path)
    {
        JsonObject file = content.Load(path, RuntimeJsonContext.Default.JsonObject);
        content.Unload(path);
        return Read(path, file, types);
    }

    /// <summary>Starts a run of the graph at <paramref name="path"/> against <paramref name="conditions"/>.</summary>
    public FlowRun Start(string path, ConditionContext conditions)
    {
        FlowGraph graph = Load(path);
        FlowRun run = new(graph, conditions);
        run.Start();
        return run;
    }

    public static FlowGraph Read(string path, JsonObject file, DataTypes types)
    {
        JsonTypeInfo<FlowGraph> typeInfo = (JsonTypeInfo<FlowGraph>)types.Options.GetTypeInfo(typeof(FlowGraph));
        try
        {
            return file.Deserialize(typeInfo) ?? throw new InvalidDataException($"{path} holds no graph.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"{path} can't be read: {exception.Message}", exception);
        }
    }

    public static JsonObject GetSchema(DataTypes types)
    {
        JsonTypeInfo typeInfo = types.Options.GetTypeInfo(typeof(FlowGraph));
        return JsonSchemas.Create(typeInfo);
    }
}
