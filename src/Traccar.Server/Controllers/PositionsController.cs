using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>Mirrors Java's PositionResource, including GPX/KML/KMZ/CSV export endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/positions")]
public class PositionsController(TraccarDbContext db) : ControllerBase
{
    // -------------------------------------------------------------------------
    // GET — list / filter positions
    // -------------------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<List<Position>>> Get(
        [FromQuery] long deviceId = 0,
        [FromQuery(Name = "id")] List<long>? ids = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        if (ids is { Count: > 0 })
        {
            return await db.Positions.Where(p => ids.Contains(p.Id)).ToListAsync();
        }

        if (deviceId > 0)
        {
            if (from.HasValue && to.HasValue)
            {
                return await db.Positions
                    .Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to)
                    .OrderBy(p => p.FixTime)
                    .ToListAsync();
            }

            var positionId = await db.Devices
                .Where(d => d.Id == deviceId)
                .Select(d => d.PositionId)
                .FirstOrDefaultAsync();

            return await db.Positions.Where(p => p.Id == positionId).ToListAsync();
        }

        var latestIds = await db.Devices
            .Where(d => d.PositionId != 0)
            .Select(d => d.PositionId)
            .ToListAsync();

        return await db.Positions.Where(p => latestIds.Contains(p.Id)).ToListAsync();
    }

    // -------------------------------------------------------------------------
    // DELETE — single position by id
    // -------------------------------------------------------------------------

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteById(long id)
    {
        var position = await db.Positions.FindAsync(id);
        if (position == null)
        {
            return NotFound();
        }
        db.Positions.Remove(position);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // DELETE — bulk by deviceId + time range
    // -------------------------------------------------------------------------

    [HttpDelete]
    public async Task<IActionResult> Delete(
        [FromQuery] long deviceId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var positions = await db.Positions
            .Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to)
            .ToListAsync();

        db.Positions.RemoveRange(positions);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // GET gpx — GPX track export
    // -------------------------------------------------------------------------

    [HttpGet("gpx")]
    public async Task<IActionResult> GetGpx(
        [FromQuery] long deviceId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var device = await db.Devices.FindAsync(deviceId);
        if (device == null) return NotFound();

        var positions = await db.Positions
            .Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to)
            .OrderBy(p => p.FixTime)
            .ToListAsync();

        var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = false };
        using (var writer = XmlWriter.Create(ms, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("gpx");
            writer.WriteAttributeString("version", "1.0");
            writer.WriteStartElement("trk");
            writer.WriteElementString("name", device.Name ?? device.UniqueId);
            writer.WriteStartElement("trkseg");
            foreach (var p in positions)
            {
                writer.WriteStartElement("trkpt");
                writer.WriteAttributeString("lat", p.Latitude.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("lon", p.Longitude.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("ele", p.Altitude.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("time", (p.FixTime ?? p.ServerTime).ToString("yyyy-MM-ddTHH:mm:ssZ"));
                writer.WriteEndElement(); // trkpt
            }
            writer.WriteEndElement(); // trkseg
            writer.WriteEndElement(); // trk
            writer.WriteEndElement(); // gpx
        }

        ms.Seek(0, SeekOrigin.Begin);
        return File(ms, "application/gpx+xml", "positions.gpx");
    }

    // -------------------------------------------------------------------------
    // GET kml / GET kmz — KML track export (KMZ = KML in a ZIP)
    // -------------------------------------------------------------------------

    [HttpGet("{extension:regex(^kml$|^kmz$)}")]
    public async Task<IActionResult> GetKml(
        string extension,
        [FromQuery] long deviceId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var device = await db.Devices.FindAsync(deviceId);
        if (device == null) return NotFound();

        var positions = await db.Positions
            .Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to)
            .OrderBy(p => p.FixTime)
            .ToListAsync();

        var kmlBytes = GenerateKml(device, positions, from, to);

        if (extension == "kmz")
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("doc.kml");
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(kmlBytes);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return File(ms, "application/vnd.google-earth.kmz", "positions.kmz");
        }

        return File(kmlBytes, "application/vnd.google-earth.kml+xml", "positions.kml");
    }

    private static byte[] GenerateKml(Device device, List<Position> positions, DateTime from, DateTime to)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) };
        using var writer = XmlWriter.Create(ms, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("kml", "http://www.opengis.net/kml/2.2");
        writer.WriteStartElement("Document");
        writer.WriteElementString("name", device.Name ?? device.UniqueId);
        writer.WriteStartElement("Placemark");
        writer.WriteElementString("name",
            $"{from:yyyy-MM-dd HH:mm} - {to:yyyy-MM-dd HH:mm}");
        writer.WriteStartElement("LineString");
        writer.WriteElementString("extrude", "1");
        writer.WriteElementString("tessellate", "1");
        writer.WriteElementString("altitudeMode", "absolute");
        var coords = string.Join(" ", positions.Select(p =>
            string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", p.Longitude, p.Latitude, p.Altitude)));
        writer.WriteElementString("coordinates", coords);
        writer.WriteEndElement(); // LineString
        writer.WriteEndElement(); // Placemark
        writer.WriteEndElement(); // Document
        writer.WriteEndElement(); // kml

        writer.Flush();
        return ms.ToArray();
    }

    // -------------------------------------------------------------------------
    // GET csv — CSV export
    // -------------------------------------------------------------------------

    [HttpGet("csv")]
    public async Task<IActionResult> GetCsv(
        [FromQuery] long deviceId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var positions = await db.Positions
            .Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to)
            .OrderBy(p => p.FixTime)
            .ToListAsync();

        // Collect all attribute keys present in this result set.
        var attributeKeys = positions
            .SelectMany(p => p.Attributes.Keys)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        var sb = new StringBuilder();

        // Header
        var header = new[] { "id","deviceId","protocol","serverTime","deviceTime","fixTime",
                             "valid","latitude","longitude","altitude","speed","course","address","accuracy" };
        sb.AppendLine(string.Join(",", header.Concat(attributeKeys)));

        // Rows
        const string DateFmt = "yyyy-MM-dd HH:mm:ss";
        foreach (var p in positions)
        {
            var row = new List<string>
            {
                CsvCell(p.Id),
                CsvCell(p.DeviceId),
                CsvCell(p.Protocol),
                CsvCell(p.ServerTime.ToString(DateFmt)),
                CsvCell(p.DeviceTime?.ToString(DateFmt)),
                CsvCell(p.FixTime?.ToString(DateFmt)),
                CsvCell(p.Valid),
                CsvCell(p.Latitude),
                CsvCell(p.Longitude),
                CsvCell(p.Altitude),
                CsvCell(p.Speed),
                CsvCell(p.Course),
                CsvCell(p.Address),
                CsvCell(p.Accuracy),
            };
            foreach (var key in attributeKeys)
            {
                row.Add(CsvCell(p.Attributes.GetValueOrDefault(key)));
            }
            sb.AppendLine(string.Join(",", row));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "positions.csv");
    }

    private static string CsvCell(object? value)
    {
        if (value == null) return string.Empty;
        var s = value.ToString()?.Trim() ?? string.Empty;
        // Guard against CSV injection (=, +, -, @ at start)
        if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@')
        {
            return string.Empty;
        }
        return s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;
    }
}
