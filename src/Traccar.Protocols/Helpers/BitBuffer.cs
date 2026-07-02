using DotNetty.Buffers;

namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.BitBuffer — packs/unpacks 6-bit run-length encoded fields into a byte stream.</summary>
public sealed class BitBuffer
{
    private readonly IByteBuffer _buffer;
    private int _writeByte;
    private int _writeCount;
    private int _readByte;
    private int _readCount;

    public BitBuffer() { _buffer = Unpooled.Buffer(); }

    public BitBuffer(IByteBuffer buffer) { _buffer = buffer; }

    public BitBuffer(ByteBuf buf) { _buffer = buf.Inner; }

    public void WriteEncoded(byte[] bytes)
    {
        foreach (var b in bytes)
        {
            var v = b - 48;
            if (v > 40) v -= 8;
            Write(v);
        }
    }

    public void Write(int b)
    {
        if (_writeCount == 0)
        {
            _writeByte |= b;
            _writeCount = 6;
        }
        else
        {
            int remaining = 8 - _writeCount;
            _writeByte <<= remaining;
            _writeByte |= b >> (6 - remaining);
            _buffer.WriteByte(_writeByte);
            _writeByte = b & ((1 << (6 - remaining)) - 1);
            _writeCount = 6 - remaining;
        }
    }

    public int ReadUnsigned(int length)
    {
        int result = 0;
        while (length > 0)
        {
            if (_readCount == 0)
            {
                _readByte = _buffer.ReadByte();
                _readCount = 8;
            }
            if (_readCount >= length)
            {
                result <<= length;
                result |= _readByte >> (_readCount - length);
                _readByte &= (1 << (_readCount - length)) - 1;
                _readCount -= length;
                length = 0;
            }
            else
            {
                result <<= _readCount;
                result |= _readByte;
                length -= _readCount;
                _readByte = 0;
                _readCount = 0;
            }
        }
        return result;
    }

    public int ReadSigned(int length)
    {
        int result = ReadUnsigned(length);
        int signBit = 1 << (length - 1);
        if ((result & signBit) == 0)
        {
            return result;
        }
        result &= signBit - 1;
        result += -signBit;
        return result;
    }
}
