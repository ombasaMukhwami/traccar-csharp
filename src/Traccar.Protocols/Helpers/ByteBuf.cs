using System.Text;
using DotNetty.Buffers;

namespace Traccar.Protocols.Helpers;

/// <summary>
/// Wraps DotNetty's IByteBuffer with Java Netty's ByteBuf method names and signed/unsigned value
/// semantics, so protocol decoders translated from Java can read "ReadByte" / "ReadUnsignedByte"
/// etc. directly instead of re-deriving sign/width per call site.
///
/// DotNetty's IByteBuffer doesn't distinguish ReadByte/ReadUnsignedByte the way Java's ByteBuf
/// does (both return an unsigned C# byte), and ReadUnsignedInt returns uint where Java returns a
/// widened long. This type makes both explicit and Java-shaped.
/// </summary>
public readonly struct ByteBuf(IByteBuffer buffer)
{
    public IByteBuffer Inner { get; } = buffer;

    public int ReaderIndex
    {
        get => Inner.ReaderIndex;
        set => Inner.SetReaderIndex(value);
    }

    public int WriterIndex => Inner.WriterIndex;

    public int ReadableBytes => Inner.ReadableBytes;

    public bool IsReadable() => Inner.IsReadable();

    // 8-bit
    public sbyte ReadByte() => unchecked((sbyte)Inner.ReadByte());

    public int ReadUnsignedByte() => Inner.ReadByte();

    public sbyte GetByte(int index) => unchecked((sbyte)Inner.GetByte(index));

    public int GetUnsignedByte(int index) => Inner.GetByte(index);

    // 16-bit
    public short ReadShort() => Inner.ReadShort();

    public int ReadUnsignedShort() => Inner.ReadUnsignedShort();

    public short GetShort(int index) => Inner.GetShort(index);

    public int GetUnsignedShort(int index) => Inner.GetUnsignedShort(index);

    public short ReadShortLE() => Inner.ReadShortLE();

    public int ReadUnsignedShortLE() => Inner.ReadUnsignedShortLE();

    // 24-bit
    public int ReadMedium() => Inner.ReadMedium();

    public int ReadUnsignedMedium() => Inner.ReadUnsignedMedium();

    public int GetMedium(int index) => Inner.GetMedium(index);

    public int GetUnsignedMedium(int index) => Inner.GetUnsignedMedium(index);

    // 32-bit
    public int ReadInt() => Inner.ReadInt();

    public long ReadUnsignedInt() => Inner.ReadUnsignedInt();

    public int GetInt(int index) => Inner.GetInt(index);

    public long GetUnsignedInt(int index) => Inner.GetUnsignedInt(index);

    public int ReadIntLE() => Inner.ReadIntLE();

    public long ReadUnsignedIntLE() => Inner.ReadUnsignedIntLE();

    // 64-bit
    public long ReadLong() => Inner.ReadLong();

    public long ReadLongLE() => Inner.ReadLongLE();

    public long GetLong(int index) => Inner.GetLong(index);

    // floating point
    public float ReadFloat() => Inner.ReadFloat();

    // bytes / slices / search
    public void SkipBytes(int length) => Inner.SkipBytes(length);

    public IByteBuffer ReadSlice(int length) => Inner.ReadSlice(length);

    public IByteBuffer ReadRetainedSlice(int length) => Inner.ReadRetainedSlice(length);

    public void ReadBytes(byte[] destination) => Inner.ReadBytes(destination);

    public void GetBytes(int index, byte[] destination) => Inner.GetBytes(index, destination);

    public int IndexOf(int fromIndex, int toIndex, byte value) => Inner.IndexOf(fromIndex, toIndex, value);

    public override string ToString() => Inner.ToString();

    public string ToString(Encoding encoding) => Inner.ToString(encoding);

    public string ToString(int index, int length, Encoding encoding) => Inner.ToString(index, length, encoding);

    /// <summary>Java's readCharSequence(length, charset).toString(): reads and advances.</summary>
    public string ReadString(int length, Encoding encoding)
    {
        var text = Inner.ToString(Inner.ReaderIndex, length, encoding);
        Inner.SkipBytes(length);
        return text;
    }

    /// <summary>Java's getCharSequence(index, length, charset).toString(): non-advancing.</summary>
    public string GetString(int index, int length, Encoding encoding) => Inner.ToString(index, length, encoding);

    // writes (Java's write* methods don't have the signed/unsigned ambiguity reads do)
    public void WriteByte(int value) => Inner.WriteByte(value);

    public void WriteShort(int value) => Inner.WriteShort(value);

    public void WriteInt(int value) => Inner.WriteInt(value);

    public void WriteBytes(byte[] src) => Inner.WriteBytes(src);

    public void WriteBytes(IByteBuffer src) => Inner.WriteBytes(src);
}
