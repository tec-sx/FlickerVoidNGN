using Avalonia.Headless;

namespace FlickerVoid.Editor.Tests;

/// <summary>
/// The one headless editor the tests show views in, on one UI thread as the editor runs: the theme's icons are shared objects
/// that belong to the thread that first drew them, so a second session would find them owned by another thread.
/// </summary>
internal static class HeadlessEditor
{
    private static readonly Lazy<HeadlessUnitTestSession> Shared = new(() => HeadlessUnitTestSession.StartNew(typeof(App)));

    public static HeadlessUnitTestSession Session => Shared.Value;
}
