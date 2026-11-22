namespace FlickerVoid.Tools.Generators.Tests;

public sealed class FlowNodeTypesGeneratorTests
{
    private const string Gate = """
        using FlickerVoid.Engine.Flow.Graphs;

        namespace Game.Gameplay.World;

        [FlowNodeType("openGate")]
        public sealed class OpenGateNode : FlowNode<FlowOutput>
        {
            protected override void Execute(FlowContext<FlowOutput> context) => context.Trigger(FlowOutput.Out);
        }
        """;

    private const string Bell = """
        using FlickerVoid.Engine.Flow.Graphs;

        namespace Game.Gameplay;

        [FlowNodeType("ringBell")]
        public sealed class RingBellNode : FlowNode<FlowOutput>
        {
            protected override void Execute(FlowContext<FlowOutput> context) => context.Trigger(FlowOutput.Out);
        }
        """;

    private readonly FlowNodeTypesGeneratorTestsBuilder _builder = new();

    [Fact]
    public void RegistersEveryMarkedNodeUnderItsNameInNameOrder()
    {
        (string generated, IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> errors) = _builder.WithSource(Bell).WithSource(Gate).Run();

        errors.Should().BeEmpty();
        generated.Should().Contain("namespace Game.Gameplay;");
        int gate = generated.IndexOf("global::Game.Gameplay.World.OpenGateNode>(\"openGate\")", StringComparison.Ordinal);
        int bell = generated.IndexOf("global::Game.Gameplay.RingBellNode>(\"ringBell\")", StringComparison.Ordinal);
        gate.Should().BePositive();
        bell.Should().BeGreaterThan(gate);
    }

    [Fact]
    public void WritesAnEmptyRegisterWhenTheProjectHasNoNodes()
    {
        (string generated, IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> errors) = _builder.WithSource("namespace Game.Gameplay; public sealed class Door;").Run();

        errors.Should().BeEmpty();
        generated.Should().Contain("public static void Register(").And.NotContain("RegisterDerived");
    }
}
