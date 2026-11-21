using Avalonia;
using Avalonia.Themes.Fluent;

namespace FlickerVoid.Engine.Ui.Tests.Surfaces;

/// <summary>An application with the Fluent theme, so the tests' buttons have templates to draw and hit.</summary>
internal sealed class TestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
