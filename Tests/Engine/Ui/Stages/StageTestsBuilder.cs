using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Ui.Stages;
using FlickerVoid.Engine.Ui.Surfaces;
using FlickerVoid.Engine.Ui.Tests.Surfaces;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Ui.Tests.Stages;

/// <summary>
/// Builds a stage over a game with rendering and stages on a Vulkan device with validation on, skipping the test without Vulkan
/// 1.3: a scene of a camera and an entity on a turntable spinning 80 degrees a second, dragged half a degree a pixel.
/// </summary>
internal sealed class StageTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private IGraphicsDevice? _device;
    private StageSystem? _stages;

    public StageTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        string folderName = Guid.NewGuid().ToString("N");
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
        string scene = """
            { "entities": [
                { "name": "Camera", "components": { "transform": { "position": [0, 1, 3] }, "camera": {} } },
                { "name": "Model", "components": { "transform": {}, "turntable": { "speed": 80, "dragSpeed": 0.5 } } } ] }
            """;
        File.WriteAllText(Path.Combine(_folder, "Stage.fvscene"), scene);
        Game = new Game(_loggerFactory);
    }

    public Game Game { get; }

    public IReadOnlyList<string> Errors => _logs.Errors;

    public Stage Build()
    {
        UiPlatform.Start<TestApplication>();
        IGraphicsDevice device = BuildDevice();
        Game.AddRendering();
        ContentManager content = new(_folder);
        Game.AddTurntables();
        _stages = Game.AddStages(device, content);
        return _stages.Create("Stage.fvscene");
    }

    /// <summary>Degrees the model's front (+Z) turned toward +X.</summary>
    public float Yaw(Stage stage)
    {
        Entity model = Entity.Null;
        stage.World.Query(new QueryDescription().WithAll<Turntable>(), (Entity entity) => model = entity);
        Transform transform = stage.World.Get<Transform>(model);
        Vector3 front = Vector3.Transform(Vector3.UnitZ, transform.Rotation);
        float radians = MathF.Atan2(front.X, front.Z);
        return float.RadiansToDegrees(radians);
    }

    public void Dispose()
    {
        _stages?.Dispose();
        Game.Dispose();
        _device?.Dispose();
        _loggerFactory.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private IGraphicsDevice BuildDevice()
    {
        GraphicsDeviceOptions options = new() { Validation = true };
        try
        {
            _device = new VulkanGraphicsDevice(options, _loggerFactory);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }

        return _device;
    }
}
