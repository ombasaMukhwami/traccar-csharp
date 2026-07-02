namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.DateUtil — date correction and formatting helpers.</summary>
public static class DateUtil
{
    /// <summary>
    /// Returns the closest past or future day-of-month match to <paramref name="guess"/> relative to now.
    /// Useful when a device sends only HH:MM:SS and you need to pick the right calendar day.
    /// </summary>
    public static DateTime CorrectDay(DateTime guess) => CorrectDate(DateTime.UtcNow, guess, addDay: true);

    /// <summary>
    /// Returns the closest past or future year match to <paramref name="guess"/> relative to now.
    /// </summary>
    public static DateTime CorrectYear(DateTime guess) => CorrectDate(DateTime.UtcNow, guess, addDay: false);

    private static DateTime CorrectDate(DateTime now, DateTime guess, bool addDay)
    {
        if (guess > now)
        {
            var previous = addDay ? guess.AddDays(-1) : guess.AddYears(-1);
            if (now - previous < guess - now)
            {
                return previous;
            }
        }
        else if (guess < now)
        {
            var next = addDay ? guess.AddDays(1) : guess.AddYears(1);
            if (next - now < now - guess)
            {
                return next;
            }
        }
        return guess;
    }

    public static DateTime ParseDate(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    public static string FormatDate(DateTime date) => date.ToString("yyyy-MM-ddTHH:mm:sszzz");

    public static string FormatDateLocal(DateTime date) => date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
