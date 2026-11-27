using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Tests;

public sealed class GpuTimerTests : IDisposable
{
    private static readonly Vector4 Red = new(1, 0, 0, 1);

    private readonly GpuTimerTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void HasNoTimingsBeforeItsFirstSubmission()
    {
        using IGraphicsDevice device = _builder.Build();
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();

        commands.Timings.Should().BeEmpty();
        commands.End();
    }

    [Fact]
    public void ReportsTheTimersOfItsLastSubmissionWhenItBeginsAgain()
    {
        using IGraphicsDevice device = _builder.BuildWithTimestamps();
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using ICommandList commands = device.CreateCommandList();

        _builder.SubmitTimed(device, commands, target, "Clear");
        commands.Begin();

        commands.Timings.Should().ContainSingle();
        GpuTiming timing = commands.Timings[0];
        timing.Name.Should().Be("Clear");
        timing.Depth.Should().Be(0);
        timing.StartMilliseconds.Should().Be(0);
        timing.Milliseconds.Should().BeGreaterThanOrEqualTo(0);
        commands.End();
    }

    [Fact]
    public void NestsTimersInsideTheOneThatIsOpen()
    {
        using IGraphicsDevice device = _builder.BuildWithTimestamps();
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.BeginTimer("Frame");
        commands.BeginTimer("Clear");
        GraphicsDeviceTestsBuilder.BeginRendering(commands, target, Red);
        commands.EndRendering();
        commands.EndTimer();
        commands.EndTimer();
        commands.End();
        device.Submit(commands);
        commands.Begin();

        commands.Timings.Select(timing => timing.Name).Should().Equal("Frame", "Clear");
        commands.Timings.Select(timing => timing.Depth).Should().Equal(0, 1);
        GpuTiming frame = commands.Timings[0];
        GpuTiming clear = commands.Timings[1];
        clear.StartMilliseconds.Should().BeGreaterThanOrEqualTo(frame.StartMilliseconds);
        (clear.StartMilliseconds + clear.Milliseconds).Should().BeLessThanOrEqualTo(frame.StartMilliseconds + frame.Milliseconds + 1e-6);
        commands.End();
    }

    [Fact]
    public void KeepsOnlyTheTimingsOfTheLastSubmission()
    {
        using IGraphicsDevice device = _builder.BuildWithTimestamps();
        using ITexture target = GraphicsDeviceTestsBuilder.CreateTarget(device);
        using ICommandList commands = device.CreateCommandList();

        _builder.SubmitTimed(device, commands, target, "First");
        _builder.SubmitTimed(device, commands, target, "Second");
        commands.Begin();

        commands.Timings.Select(timing => timing.Name).Should().Equal("Second");
        commands.End();
    }

    [Fact]
    public void DropsTimersBeyondItsCapacity()
    {
        using IGraphicsDevice device = _builder.BuildWithTimestamps();
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        for (int index = 0; index <= GraphicsLimits.TimersPerCommandList; index++)
        {
            commands.BeginTimer($"Timer {index}");
            commands.EndTimer();
        }

        commands.End();
        device.Submit(commands);
        commands.Begin();

        commands.Timings.Should().HaveCount(GraphicsLimits.TimersPerCommandList);
        commands.End();
    }

    [Fact]
    public void ThrowsWhenATimerEndsThatWasNotBegun()
    {
        using IGraphicsDevice device = _builder.Build();
        using ICommandList commands = device.CreateCommandList();
        commands.Begin();

        Action end = commands.EndTimer;

        end.Should().Throw<InvalidOperationException>();
        commands.End();
    }

    [Fact]
    public void ThrowsWhenRecordingEndsWithATimerOpen()
    {
        using IGraphicsDevice device = _builder.Build();
        using ICommandList commands = device.CreateCommandList();
        commands.Begin();
        commands.BeginTimer("Open");

        Action end = commands.End;

        end.Should().Throw<InvalidOperationException>();
        commands.EndTimer();
        commands.End();
    }
}
