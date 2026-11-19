using System.Collections.Concurrent;

using Arch.System;

using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Debugging;

/// <summary>Runs lines typed in a terminal through the debug console once per frame and prints what they show, until the game has its own console screen.</summary>
public sealed class TerminalConsoleSystem(IDebugConsole console, TextReader input, TextWriter output) : ISystem<GameTime>
{
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>Starts reading the input on a background thread, which ends when the input does.</summary>
    public void Initialize()
    {
        Thread reader = new(ReadLines) { IsBackground = true, Name = "Terminal console" };
        reader.Start();
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        while (_lines.TryDequeue(out string? line))
        {
            string shown = console.Execute(line);
            if (shown.Length > 0)
            {
                output.WriteLine(shown);
            }
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }

    private void ReadLines()
    {
        while (input.ReadLine() is { } line)
        {
            _lines.Enqueue(line);
        }
    }
}
