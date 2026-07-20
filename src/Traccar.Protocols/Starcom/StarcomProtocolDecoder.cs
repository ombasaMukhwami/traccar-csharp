using System.Globalization;
using System.Net;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Starcom;

public sealed class StarcomProtocolDecoder(ConnectionManager connectionManager, ILogger<StarcomProtocolDecoder> logger)
    : BaseProtocolDecoder("starcom", connectionManager, logger)
{
    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var sentence = ((IByteBuffer)message).ToString(Encoding.ASCII);

        // Strip outer pipes: |content|\r\n  →  content
        var first = sentence.IndexOf('|');
        var last = sentence.LastIndexOf('|');
        if (first < 0 || last <= first) return null;
        sentence = sentence[(first + 1)..last];

        var position = new Position(ProtocolName);

        foreach (var entry in sentence.Split(','))
        {
            var delim = entry.IndexOf('=');
            if (delim < 0) continue;
            var key = entry[..delim];
            var val = entry[(delim + 1)..];

            switch (key)
            {
                case "unit":
                    var session = GetDeviceSession(channel, remoteAddress, val);
                    if (session != null) position.DeviceId = session.DeviceId;
                    break;
                case "gps_valid":
                    position.Valid = int.Parse(val) != 0;
                    break;
                case "datetime_actual":
                    if (DateTime.TryParseExact(val, "yyyy/MM/dd HH:mm:ss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var dt) && dt.Year >= 2000)
                    {
                        position.SetTime(dt);
                    }
                    break;
                case "latitude":
                    position.Latitude = double.Parse(val, CultureInfo.InvariantCulture);
                    break;
                case "longitude":
                    position.Longitude = double.Parse(val, CultureInfo.InvariantCulture);
                    break;
                case "altitude":
                    position.Altitude = double.Parse(val, CultureInfo.InvariantCulture);
                    break;
                case "velocity":
                    position.Speed = UnitsConverter.KnotsFromKph(int.Parse(val));
                    break;
                case "heading":
                    position.Course = int.Parse(val);
                    break;
                case "eventid":
                    position.Set(Position.KeyEvent, int.Parse(val));
                    break;
                case "odometer":
                    position.Set(Position.KeyOdometer, (long)(double.Parse(val, CultureInfo.InvariantCulture) * 1000));
                    break;
                case "satellites":
                    position.Set(Position.KeySatellites, int.Parse(val));
                    break;
                case "ignition":
                    position.Set(Position.KeyIgnition, int.Parse(val) != 0);
                    break;
                case "door":
                    position.Set(Position.KeyDoor, int.Parse(val) != 0);
                    break;
                case "arm":
                    position.Set(Position.KeyArmed, int.Parse(val) != 0);
                    break;
                case "fuel":
                    position.Set(Position.KeyFuel, int.Parse(val));
                    break;
                case "rpm":
                    position.Set(Position.KeyRpm, int.Parse(val));
                    break;
                case "main_voltage":
                    position.Set(Position.KeyPower, double.Parse(val, CultureInfo.InvariantCulture));
                    break;
                case "backup_voltage":
                    position.Set(Position.KeyBattery, double.Parse(val, CultureInfo.InvariantCulture));
                    break;
                case "analog1":
                case "analog2":
                case "analog3":
                    position.Set(Position.PrefixAdc + (key[^1] - '0'),
                        double.Parse(val, CultureInfo.InvariantCulture));
                    break;
                default:
                    position.Set(key, val);
                    break;
            }
        }

        return position.DeviceId != 0 ? position : null;
    }
}
