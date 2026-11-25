using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Runtime.Loop;

/// <summary>
/// The world's time: a day and an hour of it, which pass at a speed in game hours to a real minute, 0 holding them. The
/// facts <c>Time.Hour</c> and <c>Time.Day</c> follow the clock, so conditions read the time, and writing them moves it, so
/// effects and flows set and advance it; while <c>Time.Held</c> is above 0 it stands still. Nothing expires on it: the game
/// stays player-paced.
/// </summary>
public sealed class WorldClock
{
    public const double DayHours = 24;

    /// <summary>The whole hour of the day, from 0 to 23.</summary>
    public static readonly Tag HourFact = new("Time.Hour");

    public static readonly Tag DayFact = new("Time.Day");

    /// <summary>Holds the clock while above 0, so more than one hold can be added and taken away.</summary>
    public static readonly Tag HeldFact = new("Time.Held");

    private readonly IFactDatabase _facts;
    private bool _telling;

    public WorldClock(IFactDatabase facts)
    {
        _facts = facts;
        _facts.FactsChanged += HandleFactsChanged;
    }

    public int Day { get; private set; }

    /// <summary>The hour of the day, from 0 up to 24, its minutes as the fraction.</summary>
    public double Hour { get; private set; }

    /// <summary>The game hours that pass in a real minute; 0 holds the clock.</summary>
    public float Speed { get; set; }

    public bool IsHeld => _facts.GetFact(HeldFact) > 0;

    /// <summary>Moves the clock to an hour of a day; hours outside the day roll into the days before or after it.</summary>
    public void Set(int day, double hour)
    {
        double days = Math.Floor(hour / DayHours);
        Day = day + (int)days;
        Hour = hour - (days * DayHours);
        if (Hour >= DayHours)
        {
            Hour = 0;
            Day++;
        }

        TellFacts();
    }

    /// <summary>Lets game hours pass.</summary>
    public void Advance(double hours)
    {
        Set(Day, Hour + hours);
    }

    /// <summary>Lets real time pass at the clock's speed, unless the clock is held.</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (Speed == 0 || IsHeld)
        {
            return;
        }

        Advance(elapsed.TotalMinutes * Speed);
    }

    /// <summary>Writes the facts that fell behind, so they change once a whole hour, not every step.</summary>
    private void TellFacts()
    {
        int hour = (int)Hour;
        _telling = true;
        if (_facts.GetFact(HourFact) != hour)
        {
            _facts.SetFact(HourFact, hour);
        }

        if (_facts.GetFact(DayFact) != Day)
        {
            _facts.SetFact(DayFact, Day);
        }

        _telling = false;
    }

    /// <summary>Follows the facts when something else wrote them: an effect, a flow, a restored save or a reset.</summary>
    private void HandleFactsChanged()
    {
        if (_telling)
        {
            return;
        }

        int hour = _facts.GetFact(HourFact);
        int day = _facts.GetFact(DayFact);
        int current = (int)Hour;
        if (hour == current && day == Day)
        {
            return;
        }

        double moved = Hour;
        if (hour != current)
        {
            moved = hour;
        }

        Set(day, moved);
    }
}
