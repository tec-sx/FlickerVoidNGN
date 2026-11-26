using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Assets.Tests.Environments;

public sealed class EnvironmentDocumentTests : IDisposable
{
    private readonly EnvironmentDocumentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ReadsTheEnvironmentFile()
    {
        Editor.Assets.Environments.EnvironmentDocument document = _builder.Build();

        document.Environment.Numbers["exposure"].Keys.Should().ContainSingle().Which.Value.Should().Be(0.4f);
    }

    [Fact]
    public void HoldsEditsUnsavedAndSavesThemWithItsSchema()
    {
        Editor.Assets.Environments.EnvironmentDocument document = _builder.Build();

        document.Environment.Numbers["exposure"] = new Curve([new CurveKey { Time = 18, Value = 0.1f }]);
        document.Hold();

        _builder.Content.IsUnsaved(EnvironmentDocumentTestsBuilder.Street).Should().BeTrue();
        LightingEnvironment loaded = _builder.Load();
        loaded.Numbers["exposure"].Keys[0].Time.Should().Be(18);
        _builder.Content.Save(EnvironmentDocumentTestsBuilder.Street);
        string file = _builder.ReadFile();
        file.Should().Contain("\"time\": 18");
        file.Should().Contain("\"$schema\": \".schemas/environment.schema.json\"");
    }
}
