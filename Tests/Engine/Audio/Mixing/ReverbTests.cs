using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Tests.Mixing;

public sealed class ReverbTests
{
    private static double TailEnergy(Reverb reverb, float wet)
    {
        float[] impulse = new float[AudioFormat.BlockFrames * 2];
        impulse[0] = 1;
        impulse[1] = 1;
        float[] silence = new float[AudioFormat.BlockFrames * 2];
        float[] output = new float[AudioFormat.BlockFrames * 2];
        reverb.Process(impulse, output, wet);
        double tail = 0;
        for (int block = 0; block < 200; block++)
        {
            Array.Clear(output);
            reverb.Process(silence, output, wet);
            tail += output.Sum(sample => Math.Abs(sample));
        }

        return tail;
    }

    [Fact]
    public void RingsOnAfterASoundHasStopped()
    {
        Reverb reverb = new() { Size = 0.5f };

        TailEnergy(reverb, 1).Should().BeGreaterThan(1);
    }

    [Fact]
    public void AddsNothingWithoutAWetMix()
    {
        Reverb reverb = new();

        TailEnergy(reverb, 0).Should().Be(0);
    }

    [Fact]
    public void RingsLongerInABiggerRoom()
    {
        double closet = TailEnergy(new Reverb { Size = 0 }, 1);
        double hall = TailEnergy(new Reverb { Size = 1 }, 1);

        hall.Should().BeGreaterThan(closet * 2);
    }

    [Fact]
    public void DiffersBetweenTheEars()
    {
        Reverb reverb = new() { Size = 0.5f };
        float[] input = new float[AudioFormat.BlockFrames * 2];
        input[0] = 1;
        input[1] = 1;
        float[] output = new float[AudioFormat.BlockFrames * 2];

        reverb.Process(input, output, 1);
        for (int block = 0; block < 5; block++)
        {
            Array.Clear(input);
            reverb.Process(input, output, 1);
        }

        output.Where((_, index) => index % 2 == 0).Should().NotEqual(output.Where((_, index) => index % 2 == 1));
    }
}
