using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Gameplay.Tests.Story;

/// <summary>The demo's camera direction in the main scene: the room shot, the window spot, the cellar door and the key-found cutscene.</summary>
public sealed class CameraDirectionTests : IDisposable
{
    private readonly CameraDirectionTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheSceneStartsOnTheRoomShot()
    {
        _builder.Start();

        _builder.Director.Live.Should().Be(_builder.Shot("Camera"));
    }

    [Fact]
    public void TheWindowSpotTurnsAndZoomsOutAndLeavingItComesBack()
    {
        _builder.Start();

        _builder.PutFox(-3, 3);
        bool atWindow = _builder.RunUntilLive("Window", 1);
        _builder.Story.Run(2);
        ActiveCamera window = _builder.Story.Game.World.Get<ActiveCamera>(_builder.Shot("Window"));
        float zoomed = window.Lens.OrthographicHeight;
        _builder.PutFox(2, 0);
        bool back = _builder.RunUntilLive("Camera", 1);

        atWindow.Should().BeTrue();
        zoomed.Should().BeGreaterThan(6.5f, "the window shot zooms out");
        back.Should().BeTrue();
    }

    [Fact]
    public void FindingTheKeyPlaysACutsceneThatReturnsToTheRoom()
    {
        _builder.Start();

        _builder.FindTheKey();
        bool cutscene = _builder.RunUntilLive("Key Reveal", 2);
        bool returned = _builder.RunUntilLive("Camera", 10);

        cutscene.Should().BeTrue();
        returned.Should().BeTrue();
    }

    [Fact]
    public void WithTheKeyTheCellarDoorFadesToTheCellarShot()
    {
        _builder.Start();
        _builder.FindTheKey();
        _builder.RunUntilLive("Camera", 10);

        _builder.PutFox(2.35f, -3.4f);
        bool cellar = _builder.RunUntilLive("Cellar", 3);
        _builder.PutFox(0, 0);
        bool back = _builder.RunUntilLive("Camera", 3);

        cellar.Should().BeTrue();
        back.Should().BeTrue();
    }

    [Fact]
    public void WithoutTheKeyTheCellarDoorKeepsTheRoomShot()
    {
        _builder.Start();

        _builder.PutFox(2.35f, -3.4f);
        _builder.Story.Run(2);

        _builder.Director.Live.Should().Be(_builder.Shot("Camera"));
    }
}
