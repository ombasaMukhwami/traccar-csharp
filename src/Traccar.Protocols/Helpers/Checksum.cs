using System.Text;

namespace Traccar.Protocols.Helpers;

public static class Checksum
{
    public static readonly Algorithm Crc8Egts = new(8, 0x31, 0xFF, false, false, 0x00);
    public static readonly Algorithm Crc8Rohc = new(8, 0x07, 0xFF, true, true, 0x00);
    public static readonly Algorithm Crc8Dallas = new(8, 0x31, 0x00, true, true, 0x00);

    public static readonly Algorithm Crc16Ibm = new(16, 0x8005, 0x0000, true, true, 0x0000);
    public static readonly Algorithm Crc16X25 = new(16, 0x1021, 0xFFFF, true, true, 0xFFFF);
    public static readonly Algorithm Crc16Modbus = new(16, 0x8005, 0xFFFF, true, true, 0x0000);
    public static readonly Algorithm Crc16CcittFalse = new(16, 0x1021, 0xFFFF, false, false, 0x0000);
    public static readonly Algorithm Crc16Kermit = new(16, 0x1021, 0x0000, true, true, 0x0000);
    public static readonly Algorithm Crc16Xmodem = new(16, 0x1021, 0x0000, false, false, 0x0000);

    public static readonly Algorithm Crc32Standard = new(32, 0x04C11DB7, unchecked((int)0xFFFFFFFF), true, true, unchecked((int)0xFFFFFFFF));
    public static readonly Algorithm Crc32Mpeg2 = new(32, 0x04C11DB7, unchecked((int)0xFFFFFFFF), false, false, 0x00000000);

    public sealed class Algorithm
    {
        private readonly int poly;
        internal readonly int init;
        private readonly bool refIn;
        private readonly bool refOut;
        private readonly int xorOut;
        private readonly int[] table;

        public Algorithm(int bits, int poly, int init, bool refIn, bool refOut, int xorOut)
        {
            this.poly = poly;
            this.init = init;
            this.refIn = refIn;
            this.refOut = refOut;
            this.xorOut = xorOut;
            table = bits switch
            {
                8 => InitTable8(),
                16 => InitTable16(),
                _ => InitTable32(),
            };
        }

        private int[] InitTable8()
        {
            var table = new int[256];
            for (var i = 0; i < 256; i++)
            {
                var crc = i;
                for (var j = 0; j < 8; j++)
                {
                    var bit = (crc & 0x80) != 0;
                    crc <<= 1;
                    if (bit)
                    {
                        crc ^= poly;
                    }
                }
                table[i] = crc & 0xFF;
            }
            return table;
        }

        private int[] InitTable16()
        {
            var table = new int[256];
            for (var i = 0; i < 256; i++)
            {
                var crc = i << 8;
                for (var j = 0; j < 8; j++)
                {
                    var bit = (crc & 0x8000) != 0;
                    crc <<= 1;
                    if (bit)
                    {
                        crc ^= poly;
                    }
                }
                table[i] = crc & 0xFFFF;
            }
            return table;
        }

        private int[] InitTable32()
        {
            var table = new int[256];
            for (var i = 0; i < 256; i++)
            {
                var crc = i << 24;
                for (var j = 0; j < 8; j++)
                {
                    crc = (crc & unchecked((int)0x80000000)) != 0 ? (crc << 1) ^ poly : crc << 1;
                }
                table[i] = crc;
            }
            return table;
        }

        internal int[] Table => table;

        internal bool RefIn => refIn;

        internal bool RefOut => refOut;

        internal int XorOut => xorOut;
    }

    private static int Reverse(int value, int bits)
    {
        var result = 0;
        for (var i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }

    public static int Crc16(Algorithm algorithm, ReadOnlySpan<byte> data)
    {
        var crc = algorithm.init;
        foreach (var raw in data)
        {
            var b = (int)raw;
            if (algorithm.RefIn)
            {
                b = Reverse(b, 8);
            }
            crc = (crc << 8) ^ algorithm.Table[((crc >> 8) & 0xFF) ^ b];
        }
        if (algorithm.RefOut)
        {
            crc = Reverse(crc, 16);
        }
        return (crc ^ algorithm.XorOut) & 0xFFFF;
    }

    public static int Crc8(Algorithm algorithm, ReadOnlySpan<byte> data)
    {
        var crc = algorithm.init;
        foreach (var raw in data)
        {
            var b = (int)raw;
            if (algorithm.RefIn)
            {
                b = Reverse(b, 8);
            }
            crc = algorithm.Table[(crc & 0xFF) ^ b];
        }
        if (algorithm.RefOut)
        {
            crc = Reverse(crc, 8);
        }
        return (crc ^ algorithm.XorOut) & 0xFF;
    }

    public static byte Xor(ReadOnlySpan<byte> data)
    {
        byte checksum = 0;
        foreach (var b in data)
        {
            checksum ^= b;
        }
        return checksum;
    }

    public static int Crc32(Algorithm algorithm, ReadOnlySpan<byte> data)
    {
        var crc = algorithm.init;
        foreach (var raw in data)
        {
            var b = (int)raw;
            if (algorithm.RefIn)
            {
                b = Reverse(b, 8);
            }
            crc = (crc << 8) ^ algorithm.Table[((crc >> 24) & 0xFF) ^ b];
        }
        if (algorithm.RefOut)
        {
            crc = Reverse(crc, 32);
        }
        return crc ^ algorithm.XorOut;
    }

    public static string Sum(string msg)
    {
        byte checksum = 0;
        foreach (var b in Encoding.ASCII.GetBytes(msg))
        {
            checksum = unchecked((byte)(checksum + b));
        }
        return checksum.ToString("X2");
    }

    public static long Luhn(long imei)
    {
        long checksum = 0;
        var remain = imei;

        for (var i = 0; remain != 0; i++)
        {
            var digit = remain % 10;
            if (i % 2 == 0)
            {
                digit *= 2;
                if (digit >= 10)
                {
                    digit = 1 + (digit % 10);
                }
            }
            checksum += digit;
            remain /= 10;
        }

        return (10 - (checksum % 10)) % 10;
    }
}
