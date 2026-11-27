using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class ClipCatalogTests
{
    [Fact]
    public void MeasuresAClipAndKeepsItsPeaks()
    {
        float[] samples = new float[48000];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = index < 24000 ? 0.5f : -1f;
        }

        ClipCatalog catalog = new(_ => new SoundClip(samples, 1, 48000));

        ClipInfo? info = catalog.Find("Loop.wav");

        info.Should().NotBeNull();
        info!.Seconds.Should().BeApproximately(1, 0.0001);
        info.Peaks.Should().HaveCount(ClipCatalog.PeakCount);
        info.Peaks[0].Should().BeApproximately(0.5f, 0.001f);
        info.Peaks[^1].Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void ReadsEachFileOnceAndKnowsAMissingOne()
    {
        int reads = 0;
        ClipCatalog catalog = new(file =>
        {
            reads++;
            return file == "Missing.wav" ? null : new SoundClip(new float[4800], 1, 48000);
        });

        catalog.Find("A.wav");
        catalog.Find("A.wav");
        ClipInfo? missing = catalog.Find("Missing.wav");
        catalog.Find("Missing.wav");

        reads.Should().Be(2);
        missing.Should().BeNull();
    }

    [Fact]
    public void ForgetsClipsWhenTold()
    {
        int reads = 0;
        ClipCatalog catalog = new(_ =>
        {
            reads++;
            return new SoundClip(new float[4800], 1, 48000);
        });
        catalog.Find("A.wav");

        catalog.Forget();
        catalog.Find("A.wav");

        reads.Should().Be(2);
    }
}
