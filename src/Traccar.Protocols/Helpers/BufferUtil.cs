using System.Text;

namespace Traccar.Protocols.Helpers;

public static class BufferUtil
{
    public static string ReadString(ByteBuf buf, int length)
    {
        var bytes = new byte[length];
        buf.ReadBytes(bytes);
        // Latin1, not ASCII: Netty's ByteBuf.readCharSequence(US_ASCII) widens each byte straight to
        // a char rather than substituting '?' for bytes >= 0x80 the way .NET's Encoding.ASCII does -
        // Latin1 matches that widening exactly while still decoding the 0-127 range identically.
        return Encoding.Latin1.GetString(bytes);
    }

    public static bool IsPrintable(ByteBuf buf, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var b = buf.GetByte(buf.ReaderIndex + i);
            if (b < 32 && b != '\r' && b != '\n')
            {
                return false;
            }
        }
        return true;
    }
}
