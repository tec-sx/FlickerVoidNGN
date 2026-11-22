namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>
/// Names a game's flow node type in <c>.fvflow</c> files' <c>$type</c>. The project's generated <c>FlowNodeTypes.Register</c>
/// registers every type marked with it, so a node needs no registration code of its own.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FlowNodeTypeAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
