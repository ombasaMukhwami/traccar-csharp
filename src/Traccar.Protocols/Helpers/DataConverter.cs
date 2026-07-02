namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.DataConverter — hex and Base64 encoding/decoding.</summary>
public static class DataConverter
{
    public static byte[] ParseHex(string value) => Convert.FromHexString(value);

    public static string PrintHex(byte[] data) => Convert.ToHexStringLower(data);

    public static byte[] ParseBase64(string value) => Convert.FromBase64String(value);

    public static string PrintBase64(byte[] data) => Convert.ToBase64String(data);
}
