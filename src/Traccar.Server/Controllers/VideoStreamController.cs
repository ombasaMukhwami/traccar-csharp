using Microsoft.AspNetCore.Mvc;
using Traccar.Protocols.Media;

namespace Traccar.Server.Controllers;

[ApiController]
[Route("api/stream")]
public class VideoStreamController(VideoStreamManager streamManager) : ControllerBase
{
    [HttpGet("{deviceId:long}/{channel:int}/live.m3u8")]
    public ActionResult Playlist(long deviceId, int channel)
        => Content(streamManager.GetPlaylist(deviceId, channel), "application/vnd.apple.mpegurl");

    [HttpGet("{deviceId:long}/{channel:int}/{index:int}.ts")]
    public ActionResult Segment(long deviceId, int channel, int index)
    {
        var data = streamManager.GetSegment(deviceId, channel, index);
        if (data == null)
        {
            return NotFound();
        }

        var bytes = new byte[data.ReadableBytes];
        data.GetBytes(data.ReaderIndex, bytes);
        return File(bytes, "video/mp2t");
    }
}
