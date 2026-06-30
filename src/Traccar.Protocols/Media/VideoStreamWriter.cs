using DotNetty.Buffers;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Media;

/// <summary>
/// Muxes JT1078 video frames into MPEG-TS, mirroring Java's VideoStreamWriter byte-for-byte (PAT
/// and PMT tables emitted on every key frame, single video elementary stream).
/// </summary>
public sealed class VideoStreamWriter
{
    private const int TsPacketSize = 188;
    private const int PmtPid = 0x1000;
    private const int VideoPid = 0x0100;
    private const int StreamTypeH264 = 0x1B;
    private const int StreamTypeH265 = 0x24;

    private int patContinuityCounter;
    private int pmtContinuityCounter;
    private int videoContinuityCounter;

    public void Write(IByteBuffer output, IByteBuffer nalData, long pts, bool isKeyFrame, int payloadType)
    {
        var isH265 = payloadType == 99;
        if (isKeyFrame)
        {
            WritePat(output);
            WritePmt(output, isH265);
        }

        var pts90K = pts * 90;
        var pesPacket = CreatePes(nalData, pts90K, isH265);
        WritePesPackets(output, pesPacket, isKeyFrame, pts90K);
        pesPacket.Release();
    }

    private void WritePat(IByteBuffer output)
    {
        var start = output.WriterIndex;

        // TS header
        output.WriteByte(0x47); // sync byte
        output.WriteByte(0x40); // payload unit start + PID high (0)
        output.WriteByte(0x00); // PID low (PAT = 0x0000)
        output.WriteByte(0x10 | (patContinuityCounter++ & 0x0F)); // payload only

        // pointer field
        output.WriteByte(0x00);

        // PAT table
        var tableStart = output.WriterIndex;
        output.WriteByte(0x00); // table id
        output.WriteShort(0xB00D); // section syntax indicator + section length (13)
        output.WriteShort(0x0001); // transport stream id
        output.WriteByte(0xC1); // reserved + version 0 + current
        output.WriteByte(0x00); // section number
        output.WriteByte(0x00); // last section number
        output.WriteShort(0x0001); // program number
        output.WriteShort(0xE000 | PmtPid); // reserved + PMT PID

        // CRC32
        output.WriteInt(ComputeCrc32(output, tableStart, output.WriterIndex - tableStart));

        // fill rest with 0xFF
        var remaining = TsPacketSize - (output.WriterIndex - start);
        for (var i = 0; i < remaining; i++)
        {
            output.WriteByte(0xFF);
        }
    }

    private void WritePmt(IByteBuffer output, bool isH265)
    {
        var start = output.WriterIndex;

        // TS header
        output.WriteByte(0x47);
        output.WriteShort(0x4000 | PmtPid); // payload unit start + PMT PID
        output.WriteByte(0x10 | (pmtContinuityCounter++ & 0x0F));

        // pointer field
        output.WriteByte(0x00);

        // PMT table
        var tableStart = output.WriterIndex;
        output.WriteByte(0x02); // table id
        output.WriteShort(0xB012); // section syntax indicator + section length (18)
        output.WriteShort(0x0001); // program number
        output.WriteByte(0xC1); // reserved + version 0 + current
        output.WriteByte(0x00); // section number
        output.WriteByte(0x00); // last section number
        output.WriteShort(0xE000 | VideoPid); // reserved + PCR PID
        output.WriteShort(0xF000); // reserved + program info length (0)

        // stream entry
        output.WriteByte(isH265 ? StreamTypeH265 : StreamTypeH264);
        output.WriteShort(0xE000 | VideoPid);
        output.WriteShort(0xF000); // reserved + ES info length (0)

        // CRC32
        output.WriteInt(ComputeCrc32(output, tableStart, output.WriterIndex - tableStart));

        // fill rest with 0xFF
        var remaining = TsPacketSize - (output.WriterIndex - start);
        for (var i = 0; i < remaining; i++)
        {
            output.WriteByte(0xFF);
        }
    }

    private static int ComputeCrc32(IByteBuffer buffer, int index, int length)
    {
        var bytes = new byte[length];
        buffer.GetBytes(index, bytes);
        return Checksum.Crc32(Checksum.Crc32Mpeg2, bytes);
    }

    private static IByteBuffer CreatePes(IByteBuffer nalData, long pts90K, bool isH265)
    {
        var pes = Unpooled.Buffer();

        // PES header
        pes.WriteMedium(0x000001); // start code prefix
        pes.WriteByte(0xE0); // stream id (video)

        var audSize = isH265 ? 7 : 6; // H.265 AUD NAL is 3 bytes vs H.264's 2 bytes
        var pesLength = nalData.ReadableBytes + 8 + audSize; // 3 (flags) + 5 (PTS) + AUD NAL
        pes.WriteShort(pesLength > 65535 ? 0x0000 : pesLength); // 0 = unbounded

        pes.WriteByte(0x80); // marker bits
        pes.WriteByte(0x80); // PTS only
        pes.WriteByte(0x05); // PES header data length

        // PTS encoding (5 bytes)
        pes.WriteByte((int)(0x21 | ((pts90K >> 29) & 0x0E)));
        pes.WriteByte((int)(pts90K >> 22));
        pes.WriteByte((int)(0x01 | ((pts90K >> 14) & 0xFE)));
        pes.WriteByte((int)(pts90K >> 7));
        pes.WriteByte((int)(0x01 | ((pts90K << 1) & 0xFE)));

        // access unit delimiter NAL
        pes.WriteInt(0x00000001); // start code
        if (isH265)
        {
            pes.WriteByte(0x46); // AUD NAL type 35 (35 << 1)
            pes.WriteByte(0x01); // temporal_id_plus1
            pes.WriteByte(0x50); // pic_type = 2 (I, P, B) + rbsp stop bit
        }
        else
        {
            pes.WriteByte(0x09); // AUD NAL type
            pes.WriteByte(0xF0); // primary_pic_type = 7 (any) + rbsp stop bit
        }

        pes.WriteBytes(nalData, nalData.ReaderIndex, nalData.ReadableBytes);

        return pes;
    }

    private void WritePesPackets(IByteBuffer output, IByteBuffer pesData, bool isKeyFrame, long pts90K)
    {
        var offset = 0;
        var first = true;
        var pesLength = pesData.ReadableBytes;

        while (offset < pesLength)
        {
            var packetStart = output.WriterIndex;

            // sync byte
            output.WriteByte(0x47);

            // PID with payload unit start flag
            var pidFlags = VideoPid;
            if (first)
            {
                pidFlags |= 0x4000; // payload unit start
            }
            output.WriteShort(pidFlags);

            var headerSize = 4;

            if (first && isKeyFrame)
            {
                // adaptation field with PCR and random access indicator
                output.WriteByte(0x30 | (videoContinuityCounter++ & 0x0F)); // adaptation + payload
                output.WriteByte(0x07); // adaptation field length
                output.WriteByte(0x50); // random access indicator + PCR flag
                // PCR base (33 bits) + reserved (6) + extension (9)
                output.WriteByte((int)(pts90K >> 25));
                output.WriteByte((int)(pts90K >> 17));
                output.WriteByte((int)(pts90K >> 9));
                output.WriteByte((int)(pts90K >> 1));
                output.WriteByte((int)(((pts90K & 1) << 7) | 0x7E)); // reserved bits
                output.WriteByte(0x00); // extension
                headerSize += 8;
            }
            else
            {
                var remaining = pesLength - offset;
                var available = TsPacketSize - 4;
                if (remaining < available)
                {
                    // need stuffing via adaptation field
                    var stuffingLength = available - remaining;
                    output.WriteByte(0x30 | (videoContinuityCounter++ & 0x0F));
                    if (stuffingLength == 1)
                    {
                        output.WriteByte(0x00);
                        headerSize += 1;
                    }
                    else
                    {
                        output.WriteByte(stuffingLength - 1);
                        output.WriteByte(0x00); // no flags
                        for (var i = 0; i < stuffingLength - 2; i++)
                        {
                            output.WriteByte(0xFF);
                        }
                        headerSize += stuffingLength;
                    }
                }
                else
                {
                    output.WriteByte(0x10 | (videoContinuityCounter++ & 0x0F)); // payload only
                }
            }

            var payloadSize = Math.Min(pesLength - offset, TsPacketSize - headerSize);
            output.WriteBytes(pesData, pesData.ReaderIndex + offset, payloadSize);

            // fill remaining with 0xFF
            var remaining2 = TsPacketSize - (output.WriterIndex - packetStart);
            for (var i = 0; i < remaining2; i++)
            {
                output.WriteByte(0xFF);
            }

            offset += payloadSize;
            first = false;
        }
    }
}
