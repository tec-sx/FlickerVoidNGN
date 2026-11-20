
using Microsoft.Extensions.Logging;

using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Physics.Tests;

/// <summary>Builds the physics system under test in a new game, which the test disposes.</summary>
internal sealed class PhysicsSystemTestsBuilder
{
    public PhysicsSystemTestsBuilder()
    {
        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        Game = new Game(loggerFactory);
    }

    public Game Game { get; }

    public IPhysicsSystem Build() => Game.AddPhysics();
}
