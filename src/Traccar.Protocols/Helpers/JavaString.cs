namespace Traccar.Protocols.Helpers;

public static class JavaString
{
    /// <summary>
    /// Mirrors Java's String.split(regex) default behavior: trailing empty strings are discarded,
    /// unlike .NET's Split() which keeps them. Leading/interior empty entries are preserved either way.
    /// </summary>
    public static string[] Split(string input, char delimiter)
    {
        var parts = input.Split(delimiter);
        var length = parts.Length;
        while (length > 0 && parts[length - 1].Length == 0)
        {
            length--;
        }
        return length == parts.Length ? parts : parts[..length];
    }

    /// <summary>
    /// Mirrors Java's String.trim(): strips any leading/trailing char whose code point is &lt;= U+0020,
    /// which includes NUL - unlike .NET's Trim(), which only strips Unicode whitespace (NUL isn't one).
    /// </summary>
    public static string Trim(string input)
    {
        var start = 0;
        var end = input.Length;
        while (start < end && input[start] <= ' ')
        {
            start++;
        }
        while (end > start && input[end - 1] <= ' ')
        {
            end--;
        }
        return input[start..end];
    }
}
