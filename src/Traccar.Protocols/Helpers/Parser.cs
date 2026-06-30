using System.Globalization;
using System.Text.RegularExpressions;

namespace Traccar.Protocols.Helpers;

public enum CoordinateFormat
{
    DegDeg,
    DegDegHem,
    DegHem,
    DegMinMin,
    DegMinHem,
    DegMinMinHem,
    HemDegMinMin,
    HemDeg,
    HemDegMin,
    HemDegMinHem,
}

public enum DateTimeFormat
{
    YmdHms,
    HmsDmy,
    DmyHms,
}

public sealed class Parser
{
    private readonly Match match;
    private int position;

    public Parser(Regex pattern, string input)
    {
        match = pattern.Match(input);
    }

    /// <summary>Wraps an already-found Match, e.g. from Regex.Matches() when iterating repeated occurrences.</summary>
    public Parser(Match match)
    {
        this.match = match;
        position = 1;
    }

    public bool Matches()
    {
        position = 1;
        return match.Success;
    }

    public void Skip(int number) => position += number;

    /// <summary>
    /// Compiles a pattern anchored to the full input, mirroring Java's Pattern.matches() semantics
    /// (as opposed to .NET's default partial-match behavior).
    /// </summary>
    public static Regex Compile(string pattern, RegexOptions options = RegexOptions.Singleline)
        => new($@"\A(?:{pattern})\z", options);

    public bool HasNext() => HasNext(1);

    public bool HasNext(int number)
    {
        for (var i = position; i < position + number; i++)
        {
            if (string.IsNullOrEmpty(GroupValue(i)))
            {
                position += number;
                return false;
            }
        }
        return true;
    }

    public bool HasNextAny(int number)
    {
        for (var i = position; i < position + number; i++)
        {
            if (!string.IsNullOrEmpty(GroupValue(i)))
            {
                return true;
            }
        }
        position += number;
        return false;
    }

    private string? GroupValue(int index)
    {
        var group = match.Groups[index];
        return group.Success ? group.Value : null;
    }

    public string? Next() => GroupValue(position++);

    public int? NextInt() => HasNext() ? int.Parse(Next()!, CultureInfo.InvariantCulture) : null;

    public int NextInt(int defaultValue) => HasNext() ? int.Parse(Next()!, CultureInfo.InvariantCulture) : defaultValue;

    public int? NextHexInt() => HasNext() ? Convert.ToInt32(Next()!, 16) : null;

    public int NextHexInt(int defaultValue) => HasNext() ? Convert.ToInt32(Next()!, 16) : defaultValue;

    public long? NextLong() => HasNext() ? long.Parse(Next()!, CultureInfo.InvariantCulture) : null;

    public long? NextHexLong() => HasNext() ? Convert.ToInt64(Next()!, 16) : null;

    public long NextLong(long defaultValue) => NextLong(10, defaultValue);

    public long NextLong(int radix, long defaultValue) => HasNext() ? Convert.ToInt64(Next()!, radix) : defaultValue;

    public double? NextDouble() => HasNext() ? double.Parse(Next()!, CultureInfo.InvariantCulture) : null;

    public double NextDouble(double defaultValue)
        => HasNext() ? double.Parse(Next()!, CultureInfo.InvariantCulture) : defaultValue;

    /// <summary>Consumes year, month, day, hour, minute, second groups (Java's default YMD_HMS format).</summary>
    public DateTime NextDateTime()
    {
        var year = NextInt(0);
        var month = NextInt(0);
        var day = NextInt(0);
        var hour = NextInt(0);
        var minute = NextInt(0);
        var second = NextInt(0);
        return new DateBuilder().SetDate(year, month, day).SetTime(hour, minute, second).GetDate();
    }

    public DateTime NextDateTime(DateTimeFormat format)
    {
        int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;

        switch (format)
        {
            case DateTimeFormat.HmsDmy:
                hour = NextInt(0);
                minute = NextInt(0);
                second = NextInt(0);
                day = NextInt(0);
                month = NextInt(0);
                year = NextInt(0);
                break;
            case DateTimeFormat.DmyHms:
                day = NextInt(0);
                month = NextInt(0);
                year = NextInt(0);
                hour = NextInt(0);
                minute = NextInt(0);
                second = NextInt(0);
                break;
            case DateTimeFormat.YmdHms:
            default:
                year = NextInt(0);
                month = NextInt(0);
                day = NextInt(0);
                hour = NextInt(0);
                minute = NextInt(0);
                second = NextInt(0);
                break;
        }

        if (year is >= 0 and < 100)
        {
            year += 2000;
        }

        return new DateBuilder().SetDate(year, month, day).SetTime(hour, minute, second).GetDate();
    }

    public double NextCoordinate(CoordinateFormat format = CoordinateFormat.DegMinHem)
    {
        double coordinate;
        string? hemisphere = null;

        switch (format)
        {
            case CoordinateFormat.DegDeg:
                coordinate = double.Parse(Next() + "." + Next(), CultureInfo.InvariantCulture);
                break;
            case CoordinateFormat.DegDegHem:
                coordinate = double.Parse(Next() + "." + Next(), CultureInfo.InvariantCulture);
                hemisphere = Next();
                break;
            case CoordinateFormat.DegHem:
                coordinate = NextDouble(0);
                hemisphere = Next();
                break;
            case CoordinateFormat.DegMinMin:
                coordinate = NextInt(0);
                coordinate += double.Parse(Next() + "." + Next(), CultureInfo.InvariantCulture) / 60;
                break;
            case CoordinateFormat.DegMinMinHem:
                coordinate = NextInt(0);
                coordinate += double.Parse(Next() + "." + Next(), CultureInfo.InvariantCulture) / 60;
                hemisphere = Next();
                break;
            case CoordinateFormat.HemDeg:
                hemisphere = Next();
                coordinate = NextDouble(0);
                break;
            case CoordinateFormat.HemDegMin:
                hemisphere = Next();
                coordinate = NextInt(0);
                coordinate += NextDouble(0) / 60;
                break;
            case CoordinateFormat.HemDegMinHem:
                hemisphere = Next();
                coordinate = NextInt(0);
                coordinate += NextDouble(0) / 60;
                if (HasNext())
                {
                    hemisphere = Next();
                }
                break;
            case CoordinateFormat.HemDegMinMin:
                hemisphere = Next();
                coordinate = NextInt(0);
                coordinate += double.Parse(Next() + "." + Next(), CultureInfo.InvariantCulture) / 60;
                break;
            case CoordinateFormat.DegMinHem:
            default:
                coordinate = NextInt(0);
                coordinate += NextDouble(0) / 60;
                hemisphere = Next();
                break;
        }

        if (hemisphere is "S" or "W" or "-")
        {
            coordinate = -Math.Abs(coordinate);
        }

        return coordinate;
    }
}
