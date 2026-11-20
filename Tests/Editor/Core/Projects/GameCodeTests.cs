using System.Runtime.Loader;

using FlickerVoid.Editor.Core.Projects;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Tests.Projects;

public sealed class GameCodeTests
{
    private readonly GameCodeTestsBuilder _builder = new();

    [Fact]
    public void FindsTheGameModulesOfTheBuiltGame()
    {
        IReadOnlyList<IGameModule> modules = _builder.Load();

        IGameModule module = modules.Should().ContainSingle().Subject;
        module.GetType().Name.Should().Be(nameof(SpinModule));
    }

    [Fact]
    public void LoadsTheGameApartButSharesTheEngine()
    {
        IReadOnlyList<IGameModule> modules = _builder.Load();

        AssemblyLoadContext? game = AssemblyLoadContext.GetLoadContext(modules[0].GetType().Assembly);
        AssemblyLoadContext? engine = AssemblyLoadContext.GetLoadContext(typeof(IGameModule).Assembly);
        game.Should().NotBe(AssemblyLoadContext.Default, "the project's code is the built game's, not the editor's");
        engine.Should().Be(AssemblyLoadContext.Default);
    }
}
