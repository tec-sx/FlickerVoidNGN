using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Ui.Stages;

namespace FlickerVoid.Interface;

/// <summary>What the interface's scenes need in every game and in the editor: turntables, which the hub's stage spins.</summary>
public sealed class InterfaceModule : IGameModule
{
    public void Add(Game game)
    {
        game.AddTurntables();
    }
}
