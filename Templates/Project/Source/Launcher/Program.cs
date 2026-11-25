using FlickerVoid.Engine.Hosting;
using FlickerVoid.Engine.Vehicles;

using Microsoft.Extensions.Logging;

// Runs the game in a window on its startup scene; Escape closes it. "--scene <file>" runs another scene (the editor's Play does),
// "--mood <file>" lights it with a lighting mood, "--frames <count>" closes it after that many frames and "--validation" checks Vulkan calls.
HostOptions options;
try
{
    options = HostOptions.Parse(args, "Scenes/Main.fvscene");
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
using SceneHost host = new(options, loggerFactory, "FlickerVoidGame");
host.Game.AddVehicles();
host.Run();
return 0;
