using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Tests.Projects;

/// <summary>A game module in the test assembly, which the game code tests load as a project's built game.</summary>
public sealed class SpinModule : IGameModule
{
    public void Add(Game game)
    {
        game.Spawner.Register("spin", SpinJsonContext.Default.Spin);
    }
}
