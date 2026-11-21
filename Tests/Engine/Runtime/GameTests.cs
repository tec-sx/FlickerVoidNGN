using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

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
    public void RegistersASceneSpawnerThatKnowsTransforms()
    {
        using Game game = _builder.Build();

        ISceneSpawner? spawner = game.Services.GetService<ISceneSpawner>();
        Action registerAgain = () => game.Spawner.Register("transform", RuntimeJsonContext.Default.Transform);

        spawner.Should().BeSameAs(game.Spawner);
        registerAgain.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegistersTheDebugConsole()
    {
        using Game game = _builder.Build();

        IDebugConsole? console = game.Services.GetService<IDebugConsole>();

        console.Should().BeSameAs(game.DebugConsole);
    }

    [Fact]
    public void DeliversEventsSystemsSendAfterTheirGroupRuns()
    {
        using Game game = _builder.WithFrameSystem(_frameSystem).Build();
        List<int> delivered = [];
        game.Events.Handle((in int value) => delivered.Add(value));
        _frameSystem.When(system => system.Update(Arg.Any<GameTime>())).Do(_ => game.Events.Send(7));

        game.Tick(game.Loop.FixedStep);

        delivered.Should().Equal(7);
    }

    [Fact]
    public void StopsItsEntitiesWhenDisposed()
    {
        Game game = _builder.Build();
        List<string> detached = [];
        game.Lifecycle.OnDetach((Entity _, ref EntityName name) => detached.Add(name.Value));
        Entity entity = game.World.Create(new EntityName("Lamp"));
        game.Lifecycle.Start(game.World, entity);

        game.Dispose();

        detached.Should().Equal("Lamp");
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
