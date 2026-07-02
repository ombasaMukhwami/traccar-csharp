using Traccar.Model;

namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.ObdDecoder — decodes OBD-II mode 01/02/03 PID responses.</summary>
public static class ObdDecoder
{
    private const int ModeCurrent = 0x01;
    private const int ModeFreezeFrame = 0x02;
    private const int ModeCodes = 0x03;

    public static KeyValuePair<string, object>? Decode(int mode, string value) => mode switch
    {
        ModeCurrent or ModeFreezeFrame => DecodeData(
            Convert.ToInt32(value[..2], 16),
            Convert.ToInt64(value[2..], 16), true),
        ModeCodes => DecodeCodes(value),
        _ => null,
    };

    public static KeyValuePair<string, object>? DecodeCodes(string value)
    {
        var codes = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length / 4; i++)
        {
            int numValue = Convert.ToInt32(value.Substring(i * 4, 4), 16);
            codes.Append(' ').Append(DecodeCode(numValue));
        }
        return codes.Length > 0
            ? new KeyValuePair<string, object>(Position.KeyDtcs, codes.ToString().Trim())
            : null;
    }

    public static string DecodeCode(int value)
    {
        char prefix = (value >> 14) switch { 1 => 'C', 2 => 'B', 3 => 'U', _ => 'P' };
        return $"{prefix}{value & 0x3FFF:X4}";
    }

    public static KeyValuePair<string, object>? DecodeData(int pid, long value, bool convert) => pid switch
    {
        0x04 => Pair(Position.KeyEngineLoad, convert ? value * 100 / 255 : value),
        0x05 => Pair(Position.KeyCoolantTemp, convert ? value - 40 : value),
        0x0B => Pair("mapIntake", value),
        0x0C => Pair(Position.KeyRpm, convert ? value / 4 : value),
        0x0D => Pair(Position.KeyObdSpeed, value),
        0x0F => Pair("intakeTemp", convert ? value - 40 : value),
        0x11 => Pair(Position.KeyThrottle, convert ? value * 100 / 255 : value),
        0x21 => Pair("milDistance", value),
        0x2F => Pair(Position.KeyFuel, convert ? value * 100 / 255 : value),
        0x31 => Pair("clearedDistance", value),
        _ => null,
    };

    private static KeyValuePair<string, object> Pair(string key, object value) => new(key, value);
}
