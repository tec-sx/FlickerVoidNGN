using Avalonia;
using Avalonia.Headless;

namespace FlickerVoid.Engine.Ui.Surfaces;

/// <summary>
/// Starts Avalonia once per process for the game's interface: the headless platform drawing with Skia into memory, on the thread that
/// starts it, which is the game loop's; the game's surface is then drawn over the scene.
/// </summary>
public static class UiPlatform
{
    private static readonly Lock Starting = new();

    public static bool IsStarted { get; private set; }

    /// <summary>Starts Avalonia with the game's application, which holds its styles and resources; later calls do nothing.</summary>
    public static void Start<TApplication>()
        where TApplication : Application, new()
    {
        lock (Starting)
        {
            if (IsStarted)
            {
                return;
            }

            AvaloniaHeadlessPlatformOptions options = new() { UseHeadlessDrawing = false };
            AppBuilder.Configure<TApplication>().UseSkia().UseHeadless(options).SetupWithoutStarting();
            IsStarted = true;
        }
    }

    /// <summary>Starts Avalonia with a plain application, for interfaces that bring their own styles.</summary>
    public static void Start() => Start<Application>();
}
