using FlickerVoid.Editor.Core.Projects;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Tests.Projects;

/// <summary>Loads the test assembly as if it were a project's built game.</summary>
internal sealed class GameCodeTestsBuilder
{
    public IReadOnlyList<IGameModule> Load()
    {
        string program = typeof(SpinModule).Assembly.Location;
        return GameCode.Load(program);
    }
}
