using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Runtime;

using FlickerVoidGame.Launcher;

using Microsoft.Extensions.Logging;

// Opens the game window. Escape closes it; "--frames <count>" closes it after that many frames.
int frameLimit = 0;
if (args.Length == 2 && args[0] == "--frames")
{
    frameLimit = int.Parse(args[1]);
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
using Game game = new(loggerFactory);
using GameWindow window = new(game, "FlickerVoidGame", 1280, 720);
CloseSystem close = new(window, frameLimit);
game.FrameSystems.Add(close);
window.Run();
