using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.NetworkUtil — channel session identifier helpers.</summary>
public static class NetworkUtil
{
    /// <summary>Returns a short transport-tagged session ID, e.g. "T1a2b" (TCP) or "Uc3d4" (UDP).</summary>
    public static string Session(IChannel channel)
    {
        char transport = channel is IDatagramChannel ? 'U' : 'T';
        return transport + channel.Id.AsShortText();
    }
}
