using Arch.Core;
using Arch.System;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Tests;

public class GameTests
{
    private readonly ISystem<GameTime> _fixedSystem = Substitute.For<ISystem<GameTime>>();
    private readonly ISystem<GameTime> _frameSystem = Substitute.For<ISystem<GameTime>>();
    private readonly GameTestsBuilder _builder = new();

    [Fact]
    public void RegistersWorldAndLoggerFactoryAsServices()
    {
        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        using Game game = _builder.WithLoggerFactory(loggerFactory).Build();

        World? world = game.Services.GetService<World>();
        ILoggerFactory? registered = game.Services.GetService<ILoggerFactory>();

        world.Should().BeSameAs(game.World);
        registered.Should().BeSameAs(loggerFactory);
    }

    [Fact]
    public void RunsFixedSystemsBeforeFrameSystems()
    {
        using Game game = _builder.WithFixedSystem(_fixedSystem).WithFrameSystem(_frameSystem).Build();

        game.Tick(game.Loop.FixedStep * 2);

        Received.InOrder(() =>
        {
            _fixedSystem.Initialize();
            _frameSystem.Initialize();
            _fixedSystem.BeforeUpdate(Arg.Any<GameTime>());
            _fixedSystem.Update(Arg.Any<GameTime>());
            _fixedSystem.AfterUpdate(Arg.Any<GameTime>());
            _fixedSystem.BeforeUpdate(Arg.Any<GameTime>());
            _fixedSystem.Update(Arg.Any<GameTime>());
            _fixedSystem.AfterUpdate(Arg.Any<GameTime>());
            _frameSystem.BeforeUpdate(Arg.Any<GameTime>());
            _frameSystem.Update(Arg.Any<GameTime>());
            _frameSystem.AfterUpdate(Arg.Any<GameTime>());
        });
    }

    [Fact]
    public void InitializesSystemsOnce()
    {
        using Game game = _builder.WithFrameSystem(_frameSystem).Build();

        game.Tick(TimeSpan.Zero);
        game.Tick(TimeSpan.Zero);

        _frameSystem.Received(1).Initialize();
    }

    [Fact]
    public void DisposesSystemsAndWorld()
    {
        Game game = _builder.WithFrameSystem(_frameSystem).Build();
        World world = game.World;

        game.Dispose();

        _frameSystem.Received(1).Dispose();
        World.Worlds.Should().NotContain(world);
    }
}
