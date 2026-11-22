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
        GameCode code = _builder.Load();

        IGameModule module = code.Modules.Should().ContainSingle().Subject;
        module.GetType().Name.Should().Be(nameof(SpinModule));
    }

    [Fact]
    public void LoadsTheGameApartButSharesTheEngine()
    {
        GameCode code = _builder.Load();

        AssemblyLoadContext? game = AssemblyLoadContext.GetLoadContext(code.Modules[0].GetType().Assembly);
        AssemblyLoadContext? engine = AssemblyLoadContext.GetLoadContext(typeof(IGameModule).Assembly);
        game.Should().NotBe(AssemblyLoadContext.Default, "the project's code is the built game's, not the editor's");
        engine.Should().Be(AssemblyLoadContext.Default);
    }

    [Fact]
    public void LoadsEachBuildApartSoARebuiltGameBringsNewTypes()
    {
        GameCode first = _builder.Load();
        GameCode second = _builder.Load();

        second.Modules[0].GetType().Should().NotBe(first.Modules[0].GetType());
    }

    [Fact]
    public void ReadsTheBuiltFilesIntoMemorySoARebuildCanOverwriteThem()
    {
        GameCode code = _builder.Load();

        code.Modules[0].GetType().Assembly.Location.Should().BeEmpty("an assembly loaded from a file keeps it open on Windows");
    }

    [Fact]
    public void UnloadsItsCode()
    {
        GameCode code = _builder.Load();
        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(code.Modules[0].GetType().Assembly)!;
        bool unloading = false;
        context.Unloading += _ => unloading = true;

        code.Unload();

        context.IsCollectible.Should().BeTrue();
        unloading.Should().BeTrue();
    }
}
