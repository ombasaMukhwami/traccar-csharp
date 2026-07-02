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
}
