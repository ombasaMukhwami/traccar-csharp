namespace Traccar.Protocols.Helpers;

/// <summary>
/// Builds a UTC DateTime from individually-set date/time fields. When a timeZone is supplied, the
/// fields are interpreted as local time in that zone and converted to UTC, mirroring Java's
/// DateBuilder(TimeZone) using a Calendar in the given zone.
/// </summary>
public sealed class DateBuilder(TimeZoneInfo? timeZone = null)
{
    private int _year = 1970;
    private int _month = 1;
    private int _day = 1;
    private int _hour;
    private int _minute;
    private int _second;
    private int _millisecond;

    public DateBuilder SetYear(int year)
    {
        _year = year < 100 ? year + 2000 : year;
        return this;
    }

    public DateBuilder SetMonth(int month)
    {
        _month = month;
        return this;
    }

    public DateBuilder SetDay(int day)
    {
        _day = day;
        return this;
    }

    public DateBuilder SetDate(int year, int month, int day) => SetYear(year).SetMonth(month).SetDay(day);

    public DateBuilder SetDateReverse(int day, int month, int year) => SetDate(year, month, day);

    public DateBuilder SetHour(int hour)
    {
        _hour = hour;
        return this;
    }

    public DateBuilder SetMinute(int minute)
    {
        _minute = minute;
        return this;
    }

    public DateBuilder SetSecond(int second)
    {
        _second = second;
        return this;
    }

    public DateBuilder SetMillis(int millis)
    {
        _millisecond = millis;
        return this;
    }

    public DateBuilder SetTime(int hour, int minute, int second) => SetHour(hour).SetMinute(minute).SetSecond(second);

    public DateBuilder SetTime(int hour, int minute, int second, int millis)
        => SetHour(hour).SetMinute(minute).SetSecond(second).SetMillis(millis);

    public DateBuilder SetTimeReverse(int second, int minute, int hour)
        => SetHour(hour).SetMinute(minute).SetSecond(second);

    public DateTime GetDate()
    {
        // Java's Calendar silently normalizes out-of-range fields instead of throwing, which
        // matters for garbage/misaligned bytes in malformed or vendor-specific device payloads;
        // clamping here (as the other fields already do) avoids a crash in that case.
        var safeYear = Math.Clamp(_year, 1, 9999);
        var safeMonth = Math.Clamp(_month, 1, 12);
        var safeDay = Math.Clamp(_day, 1, DateTime.DaysInMonth(safeYear, safeMonth));
        var date = new DateTime(
            safeYear, safeMonth, safeDay,
            Math.Clamp(_hour, 0, 23), Math.Clamp(_minute, 0, 59), Math.Clamp(_second, 0, 59),
            Math.Clamp(_millisecond, 0, 999), DateTimeKind.Unspecified);
        return timeZone == null
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : TimeZoneInfo.ConvertTimeToUtc(date, timeZone);
    }
}
