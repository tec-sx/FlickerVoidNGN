using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Samples.Window;

using Microsoft.Extensions.Logging;

// Opens a window, prints key presses and mouse clicks, and shows the frame rate and mouse position in the title.
// Escape closes it; "--frames <count>" closes it after that many frames.
int frameLimit = 0;
if (args.Length == 2 && args[0] == "--frames")
{
    frameLimit = int.Parse(args[1]);
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
using Game game = new(loggerFactory);
using GameWindow window = new(game, "FlickerVoid", 1280, 720);
InputEchoSystem echo = new(window, frameLimit);
game.FrameSystems.Add(echo);
window.Run();
Console.WriteLine($"Closed after {echo.Frames} frames.");
