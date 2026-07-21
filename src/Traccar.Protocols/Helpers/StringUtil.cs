namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.StringUtil.</summary>
public static class StringUtil
{
    public static bool ContainsHex(string value)
    {
        foreach (char c in value)
        {
            if (c is >= 'a' and <= 'f' or >= 'A' and <= 'F')
            {
                return true;
            }
        }
        return false;
    }

    public static string StripLeading(char c, string value)
    {
        var start = 0;
        while (start < value.Length - 1 && value[start] == c)
        {
            start++;
        }
        return value[start..];
    }

    public static string StripTrailing(char c, string value)
    {
        var end = value.Length;
        while (end > 1 && value[end - 1] == c)
        {
            end--;
        }
        return value[..end];
    }
}
