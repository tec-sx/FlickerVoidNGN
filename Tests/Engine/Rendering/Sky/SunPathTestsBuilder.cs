using FlickerVoid.Engine.Rendering.Sky;

namespace FlickerVoid.Engine.Rendering.Tests.Sky;

/// <summary>Builds a sun path from a latitude, a north and a declination, an equinox at 45 degrees north unless told.</summary>
internal sealed class SunPathTestsBuilder
{
    private float _latitude = 45;
    private float _north;
    private float _declination;

    public SunPathTestsBuilder AtLatitude(float degrees)
    {
        _latitude = degrees;
        return this;
    }

    public SunPathTestsBuilder FacingNorth(float degrees)
    {
        _north = degrees;
        return this;
    }

    public SunPathTestsBuilder WithDeclination(float degrees)
    {
        _declination = degrees;
        return this;
    }

    public SunPath Build() => new(_latitude, _north, _declination);
}
