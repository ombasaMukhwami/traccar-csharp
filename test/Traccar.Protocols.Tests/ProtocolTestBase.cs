using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Embedded;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Session;
using Traccar.Storage;
using Xunit;

namespace Traccar.Protocols.Tests;

internal sealed class TestDbContextFactory(DbContextOptions<TraccarDbContext> options) : IDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext() => new(options);
}

public abstract class ProtocolTestBase
{
    private readonly TestDbContextFactory dbContextFactory;

    protected ProtocolTestBase()
    {
        var options = new DbContextOptionsBuilder<TraccarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        dbContextFactory = new TestDbContextFactory(options);
    }

    protected IDbContextFactory<TraccarDbContext> DbContextFactory => dbContextFactory;

    protected ConnectionManager CreateConnectionManager()
        => new(dbContextFactory, NullLogger<ConnectionManager>.Instance);

    protected static IConfiguration CreateConfiguration() => new ConfigurationBuilder().Build();

    protected long SeedDevice(string uniqueId, string? model = null)
    {
        using var db = dbContextFactory.CreateDbContext();
        var device = new Device { Name = uniqueId, UniqueId = uniqueId, Model = model };
        db.Devices.Add(device);
        db.SaveChanges();
        return device.Id;
    }

    protected static IByteBuffer Buffer(string text) => Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(text));

    protected static IByteBuffer Binary(string hex) => Unpooled.WrappedBuffer(Convert.FromHexString(hex));

    protected static object? Decode(ChannelHandlerAdapter decoder, IByteBuffer input)
    {
        var channel = new EmbeddedChannel(decoder);
        channel.WriteInbound(input);
        return channel.ReadInbound<object>();
    }

    protected static List<Position> DecodeAll(ChannelHandlerAdapter decoder, IByteBuffer input)
    {
        var channel = new EmbeddedChannel(decoder);
        channel.WriteInbound(input);
        var results = new List<Position>();
        object? item;
        while ((item = channel.ReadInbound<object>()) != null)
        {
            results.Add(Assert.IsType<Position>(item));
        }
        return results;
    }

    protected static Position VerifyPosition(ChannelHandlerAdapter decoder, IByteBuffer input)
    {
        var result = Decode(decoder, input);
        var position = Assert.IsType<Position>(result);
        Assert.InRange(position.Latitude, -90, 90);
        Assert.InRange(position.Longitude, -180, 180);
        Assert.NotEqual(default, position.FixTime);
        return position;
    }

    protected static void VerifyPosition(
        ChannelHandlerAdapter decoder, IByteBuffer input, DateTime expectedTime, bool expectedValid,
        double expectedLatitude, double expectedLongitude)
    {
        var position = VerifyPosition(decoder, input);
        Assert.Equal(expectedTime, position.FixTime);
        Assert.Equal(expectedValid, position.Valid);
        Assert.Equal(expectedLatitude, position.Latitude, 0.001);
        Assert.Equal(expectedLongitude, position.Longitude, 0.001);
    }

    protected static void VerifyNull(ChannelHandlerAdapter decoder, IByteBuffer input)
    {
        Assert.Null(Decode(decoder, input));
    }

    protected static void VerifyFrame(ChannelHandlerAdapter decoder, IByteBuffer expected, IByteBuffer input)
    {
        var buffer = Assert.IsAssignableFrom<IByteBuffer>(Decode(decoder, input));
        Assert.Equal(ByteBufferUtil.HexDump(expected), ByteBufferUtil.HexDump(buffer));
    }

    protected static void VerifyNotNull(ChannelHandlerAdapter decoder, IByteBuffer input)
    {
        Assert.NotNull(Decode(decoder, input));
    }

    protected static void VerifyAttribute(ChannelHandlerAdapter decoder, IByteBuffer input, string key, object expected)
    {
        var result = Decode(decoder, input);
        var position = result is IEnumerable<Position> positions
            ? positions.First()
            : Assert.IsType<Position>(result);
        Assert.True(position.HasAttribute(key));
        Assert.Equal(Convert.ToString(expected), Convert.ToString(position.Attributes[key]));
    }

    protected static void VerifyAttributes(ChannelHandlerAdapter decoder, IByteBuffer input)
    {
        var position = Assert.IsType<Position>(Decode(decoder, input));
        Assert.NotEmpty(position.Attributes);
    }

    protected static void VerifyPositions(ChannelHandlerAdapter decoder, IByteBuffer input)
    {
        var positions = DecodeAll(decoder, input);
        Assert.NotEmpty(positions);
        foreach (var position in positions)
        {
            Assert.InRange(position.Latitude, -90, 90);
            Assert.InRange(position.Longitude, -180, 180);
        }
    }

    protected static object? EncodeCommand(ChannelHandlerAdapter encoder, Command command)
    {
        var channel = new EmbeddedChannel(encoder);
        channel.WriteOutbound(command);
        return channel.ReadOutbound<object>();
    }

    protected static string EncodeCommandAsHex(ChannelHandlerAdapter encoder, Command command)
    {
        var buf = Assert.IsAssignableFrom<IByteBuffer>(EncodeCommand(encoder, command));
        return ByteBufferUtil.HexDump(buf).ToLowerInvariant();
    }
}
