using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Sky;

namespace FlickerVoid.Engine.Rendering.Tests.Sky;

public sealed class SunPathTests
{
    private const float Precision = 1e-4f;

    private readonly SunPathTestsBuilder _builder = new();

    [Fact]
    public void StandsInTheSouthAtNoonNorthOfTheEquator()
    {
        SunPath path = _builder.AtLatitude(45).Build();

        Vector3 toSun = path.TowardSun(12);

        toSun.Y.Should().BeApproximately(MathF.Sqrt(0.5f), Precision, "at 45 degrees north the noon sun of an equinox stands 45 degrees up");
        toSun.Z.Should().BeApproximately(MathF.Sqrt(0.5f), Precision, "north is -Z, so the south is +Z");
        toSun.X.Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void RisesInTheEastAndSetsInTheWest()
    {
        SunPath path = _builder.AtLatitude(45).Build();

        Vector3 dawn = path.TowardSun(6);
        Vector3 dusk = path.TowardSun(18);

        dawn.X.Should().BeApproximately(1, Precision, "east is +X while north is -Z");
        dawn.Y.Should().BeApproximately(0, Precision, "an equinox sun rises at six");
        dusk.X.Should().BeApproximately(-1, Precision);
    }

    [Fact]
    public void StandsOverheadAtNoonOnTheEquator()
    {
        SunPath path = _builder.AtLatitude(0).Build();

        Vector3 toSun = path.TowardSun(12);

        toSun.Y.Should().BeApproximately(1, Precision);
    }

    [Fact]
    public void IsBelowTheHorizonAtMidnight()
    {
        SunPath path = _builder.AtLatitude(45).Build();

        Vector3 toSun = path.TowardSun(0);

        toSun.Y.Should().BeApproximately(-MathF.Sqrt(0.5f), Precision);
    }

    [Fact]
    public void TurnsWithTheScenesNorth()
    {
        SunPath path = _builder.AtLatitude(45).FacingNorth(90).Build();

        Vector3 dawn = path.TowardSun(6);

        dawn.Z.Should().BeApproximately(1, Precision, "with north turned to +X, east turns to +Z");
    }

    [Fact]
    public void ClimbsHigherInSummer()
    {
        SunPath equinox = _builder.AtLatitude(45).Build();
        SunPath summer = new SunPathTestsBuilder().AtLatitude(45).WithDeclination(23.44f).Build();

        float equinoxHeight = equinox.TowardSun(12).Y;
        float summerHeight = summer.TowardSun(12).Y;

        summerHeight.Should().BeGreaterThan(equinoxHeight);
    }

    [Fact]
    public void WritesTheLightsDirectionAwayFromTheSun()
    {
        SunPath path = _builder.AtLatitude(45).Build();
        LightingMood mood = new();

        path.Apply(0, 9, mood);

        Vector3 toSun = path.TowardSun(9);
        mood.SunDirection.X.Should().BeApproximately(-toSun.X, Precision);
        mood.SunDirection.Y.Should().BeApproximately(-toSun.Y, Precision);
        mood.SunDirection.Z.Should().BeApproximately(-toSun.Z, Precision);
    }

    [Fact]
    public void PutsAFullMoonOppositeTheSun()
    {
        SunPath path = _builder.AtLatitude(45).Build();
        int fullMoonDay = (int)MathF.Round(SunPath.MoonMonthDays / 2);

        Vector3 toMoon = path.TowardMoon(fullMoonDay, 0);

        toMoon.Y.Should().BeGreaterThan(0.6f, "a full moon stands high at midnight");
        Vector3.Dot(toMoon, path.TowardSun(0)).Should().BeLessThan(-0.99f);
    }

    [Fact]
    public void PutsANewMoonBesideTheSun()
    {
        SunPath path = _builder.AtLatitude(45).Build();

        Vector3 toMoon = path.TowardMoon(0, 12);

        Vector3.Dot(toMoon, path.TowardSun(12)).Should().BeGreaterThan(0.99f);
    }

    [Fact]
    public void RisesLaterEachDay()
    {
        SunPath path = _builder.AtLatitude(45).Build();

        Vector3 today = path.TowardMoon(10, 21);
        Vector3 tomorrow = path.TowardMoon(11, 21);

        tomorrow.X.Should().BeGreaterThan(today.X, "a day later the moon stands farther back toward the east at the same hour");
    }

    [Fact]
    public void WritesTheMoonlightsDirectionAwayFromTheMoon()
    {
        SunPath path = _builder.AtLatitude(45).Build();
        LightingMood mood = new();

        path.Apply(15, 1, mood);

        Vector3 toMoon = path.TowardMoon(15, 1);
        mood.MoonDirection.X.Should().BeApproximately(-toMoon.X, Precision);
        mood.MoonDirection.Y.Should().BeApproximately(-toMoon.Y, Precision);
        mood.MoonDirection.Z.Should().BeApproximately(-toMoon.Z, Precision);
    }

    [Fact]
    public void PointsThePoleNorthAsHighAsTheLatitude()
    {
        SunPath path = _builder.AtLatitude(45).Build();

        Vector3 pole = path.Pole;

        pole.Y.Should().BeApproximately(MathF.Sqrt(0.5f), Precision);
        pole.Z.Should().BeApproximately(-MathF.Sqrt(0.5f), Precision, "north is -Z");
    }

    [Fact]
    public void WritesThePoleTheStarsTurnAbout()
    {
        SunPath path = _builder.AtLatitude(30).FacingNorth(90).Build();
        LightingMood mood = new();

        path.Apply(0, 0, mood);

        mood.StarPole.Should().Be(path.Pole);
    }
}
