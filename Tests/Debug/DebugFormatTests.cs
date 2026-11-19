namespace FlickerVoid.Engine.Debug.Tests;

public sealed class DebugFormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KiB")]
    [InlineData(2 * 1024 * 1024, "2.0 MiB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3.0 GiB")]
    public void WritesBytesInTheLargestUnitThatKeepsThemAboveOne(long bytes, string expected)
    {
        string text = DebugFormat.Bytes(bytes);

        text.Should().Be(expected);
    }
}
