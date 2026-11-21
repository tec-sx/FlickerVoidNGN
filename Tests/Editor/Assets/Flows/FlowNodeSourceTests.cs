using FlickerVoid.Editor.Assets.Flows;

namespace FlickerVoid.Editor.Assets.Tests.Flows;

public sealed class FlowNodeSourceTests : IDisposable
{
    private readonly FlowNodeSourceTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData("Open Gate", "OpenGateNode", "openGate")]
    [InlineData("open gate", "OpenGateNode", "openGate")]
    [InlineData("Wait Node", "WaitNode", "wait")]
    [InlineData("Ring the bell!", "RingTheBellNode", "ringTheBell")]
    public void NamesTheClassAndItsTypeAfterTheName(string name, string typeName, string typeId)
    {
        FlowNodeSource.GetTypeName(name).Should().Be(typeName);
        FlowNodeSource.GetTypeId(typeName).Should().Be(typeId);
    }

    [Fact]
    public void WritesANodeClassInTheNamespaceOfItsProjectAndFolder()
    {
        _builder.WithProject();
        string folder = _builder.GetPath("Gameplay/World/Doors");

        string file = FlowNodeSource.Create(folder, "Open Gate");

        file.Should().Be(_builder.GetPath("Gameplay/World/Doors/OpenGateNode.cs"));
        string text = File.ReadAllText(file);
        text.Should().Contain("namespace Cellar.Gameplay.World.Doors;");
        text.Should().Contain("[FlowNodeType(\"openGate\")]");
        text.Should().Contain("public sealed class OpenGateNode : FlowNode<FlowOutput>");
        text.Should().Contain("context.Trigger(FlowOutput.Out);");
    }

    [Fact]
    public void AddsTheNodeToTheProjectsJsonContext()
    {
        _builder.WithProject();
        string folder = _builder.GetPath("Gameplay/World/Doors");

        FlowNodeSource.Create(folder, "Open Gate");

        string context = _builder.ReadFile("Gameplay/Serialization/CellarJsonContext.cs");
        context.Should().Contain("""
            [JsonSerializable(typeof(Barrel))]
            [JsonSerializable(typeof(global::Cellar.Gameplay.World.Doors.OpenGateNode))]
            public sealed partial class CellarJsonContext
            """);
    }

    [Fact]
    public void RefusesAFolderOutsideAProject()
    {
        _builder.WithProject();

        Action create = () => FlowNodeSource.Create(_builder.Root, "Open Gate");

        create.Should().Throw<InvalidOperationException>().WithMessage("*C# project*");
    }

    [Fact]
    public void RefusesAProjectWithoutAJsonContext()
    {
        _builder.WithFile("Gameplay/Cellar.Gameplay.csproj", "<Project />");

        Action create = () => FlowNodeSource.Create(_builder.GetPath("Gameplay"), "Open Gate");

        create.Should().Throw<InvalidOperationException>().WithMessage("*JsonSerializerContext*");
    }

    [Fact]
    public void RefusesAClassThatIsAlreadyThere()
    {
        _builder.WithProject().WithFile("Gameplay/OpenGateNode.cs");

        Action create = () => FlowNodeSource.Create(_builder.GetPath("Gameplay"), "Open Gate");

        create.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
    }
}
