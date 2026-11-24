using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Tests;

public class GameTests
{
    private readonly ISystem<GameTime> _fixedSystem = Substitute.For<ISystem<GameTime>>();
    private readonly ISystem<GameTime> _frameSystem = Substitute.For<ISystem<GameTime>>();
    private static readonly Tag HubPause = new("Pause.Hub");
    private static readonly Tag MenuPause = new("Pause.Menu");

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
    public void MovesNestedEntitiesWithTheirParentsAsItTicks()
    {
        using Game game = _builder.Build();
        Entity parent = game.World.Create(new EntityName("Trigger"), new Transform());
        Transform start = new() { Position = new Vector3(1, 0, 0) };
        Entity child = game.World.Create(new EntityName("Enter"), start, new EntityParent("Trigger"));
        game.Tick(TimeSpan.FromMilliseconds(20));

        Transform moved = new() { Position = new Vector3(0, 0, 2) };
        game.World.Set(parent, moved);
        game.Tick(TimeSpan.FromMilliseconds(20));

        game.World.Get<Transform>(child).Position.Should().Be(new Vector3(1, 0, 2));
        game.Services.GetService<TransformHierarchy>().Should().BeSameAs(game.Hierarchy);
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
    public void PausingStopsFixedSystemsUntilEveryReasonResumes()
    {
        using Game game = _builder.WithFixedSystem(_fixedSystem).WithFrameSystem(_frameSystem).Build();
        game.Pause(HubPause);
        game.Pause(MenuPause);
        game.Tick(game.Loop.FixedStep);
        game.Resume(HubPause);
        game.Tick(game.Loop.FixedStep);
        bool pausedWithOneLeft = game.IsPaused;

        game.Resume(MenuPause);
        game.Tick(game.Loop.FixedStep);

        pausedWithOneLeft.Should().BeTrue();
        game.IsPaused.Should().BeFalse();
        _fixedSystem.Received(1).Update(Arg.Any<GameTime>());
        _frameSystem.Received(2).Update(Arg.Is<GameTime>(time => time.Paused));
        _frameSystem.Received(1).Update(Arg.Is<GameTime>(time => !time.Paused));
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
