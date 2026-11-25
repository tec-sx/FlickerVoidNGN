using Avalonia;
using Avalonia.Headless;

namespace FlickerVoid.Editor.Tests.Assets;

/// <summary>Runs checks of the asset types' looks on a headless UI thread, where Avalonia can parse their icons.</summary>
internal sealed class AssetTypeLooksTestsBuilder
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(TestApplication)));

    public void Run(Action test)
    {
        Task run = Session.Value.Dispatch(test, CancellationToken.None);
        run.GetAwaiter().GetResult();
    }

    private sealed class TestApplication : Application;
}
