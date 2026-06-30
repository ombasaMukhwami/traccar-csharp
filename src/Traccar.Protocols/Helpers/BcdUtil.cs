namespace Traccar.Protocols.Helpers;

public static class BcdUtil
{
    public static int ReadInteger(ByteBuf buf, int digits)
    {
        var result = 0;
        for (var i = 0; i < digits / 2; i++)
        {
            var b = buf.ReadUnsignedByte();
            result = result * 10 + (b >> 4);
            result = result * 10 + (b & 0x0F);
        }
        if (digits % 2 != 0)
        {
            var b = buf.GetUnsignedByte(buf.ReaderIndex);
            result = result * 10 + (b >> 4);
        }
        return result;
    }

    public static double ReadCoordinate(ByteBuf buf)
    {
        var b1 = buf.ReadUnsignedByte();
        var b2 = buf.ReadUnsignedByte();
        var b3 = buf.ReadUnsignedByte();
        var b4 = buf.ReadUnsignedByte();

        double value = (b2 & 0xf) * 10 + (b3 >> 4);
        value += (((b3 & 0xf) * 10 + (b4 >> 4)) * 10 + (b4 & 0xf)) / 1000.0;
        value /= 60;
        value += ((b1 >> 4 & 0x7) * 10 + (b1 & 0xf)) * 10 + (b2 >> 4);

        if ((b1 & 0x80) != 0)
        {
            value = -value;
        }

        return value;
    }
}
