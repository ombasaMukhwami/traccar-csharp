namespace Traccar.Protocols.Helpers;

/// <summary>
/// Builds a UTC DateTime from individually-set date/time fields. When a timeZone is supplied, the
/// fields are interpreted as local time in that zone and converted to UTC, mirroring Java's
/// DateBuilder(TimeZone) using a Calendar in the given zone.
/// </summary>
public sealed class DateBuilder(TimeZoneInfo? timeZone = null)
{
    private int year = 1970;
    private int month = 1;
    private int day = 1;
    private int hour;
    private int minute;
    private int second;
    private int millisecond;

    public DateBuilder SetYear(int year)
    {
        this.year = year < 100 ? year + 2000 : year;
        return this;
    }

    public DateBuilder SetMonth(int month)
    {
        this.month = month;
        return this;
    }

    public DateBuilder SetDay(int day)
    {
        this.day = day;
        return this;
    }

    public DateBuilder SetDate(int year, int month, int day) => SetYear(year).SetMonth(month).SetDay(day);

    public DateBuilder SetDateReverse(int day, int month, int year) => SetDate(year, month, day);

    public DateBuilder SetHour(int hour)
    {
        this.hour = hour;
        return this;
    }

    public DateBuilder SetMinute(int minute)
    {
        this.minute = minute;
        return this;
    }

    public DateBuilder SetSecond(int second)
    {
        this.second = second;
        return this;
    }

    public DateBuilder SetMillis(int millis)
    {
        millisecond = millis;
        return this;
    }

    public DateBuilder SetTime(int hour, int minute, int second) => SetHour(hour).SetMinute(minute).SetSecond(second);

    public DateBuilder SetTime(int hour, int minute, int second, int millis)
        => SetHour(hour).SetMinute(minute).SetSecond(second).SetMillis(millis);

    public DateBuilder SetTimeReverse(int second, int minute, int hour)
        => SetHour(hour).SetMinute(minute).SetSecond(second);

    public DateTime GetDate()
    {
        var safeMonth = Math.Clamp(month, 1, 12);
        var safeDay = Math.Clamp(day, 1, DateTime.DaysInMonth(year, safeMonth));
        var date = new DateTime(
            year, safeMonth, safeDay,
            Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), Math.Clamp(second, 0, 59),
            Math.Clamp(millisecond, 0, 999), DateTimeKind.Unspecified);
        return timeZone == null
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : TimeZoneInfo.ConvertTimeToUtc(date, timeZone);
    }
}
