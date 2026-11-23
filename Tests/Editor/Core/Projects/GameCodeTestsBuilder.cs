using FlickerVoid.Editor.Core.Projects;

namespace FlickerVoid.Editor.Core.Tests.Projects;

/// <summary>Loads the test assembly as if it were a project's built game.</summary>
internal sealed class GameCodeTestsBuilder
{
    public GameCode Load()
    {
        string program = typeof(SpinModule).Assembly.Location;
        return GameCode.Load(program);
    }
}
