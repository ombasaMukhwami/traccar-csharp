using System.Text.Json;
using System.Text.Json.Serialization;

namespace Traccar.Model;

/// <summary>Port of the Blazor frontend's Geozone DTO — a client-scoped map zone drawn on the
/// live map. <see cref="Data"/> is stored as JSON text (see TraccarDbContext), matching how
/// every other JSON-shaped column in this codebase (Attributes, Network, ...) is persisted.</summary>
public class Geozone
{
    public int Id { get; set; }

    public string GeozoneName { get; set; } = string.Empty;

    /// <summary>One of "circle", "rectangle", "polygon" (matches the frontend's GeozoneTypes).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The drawn shape, as a GeoJSON Feature.</summary>
    public GeozoneShape Data { get; set; } = new();

    public int? ResellerId { get; set; }

    public int? ClientId { get; set; }

    public string? UserId { get; set; }
}

/// <summary>A GeoJSON Feature.</summary>
public class GeozoneShape
{
    public string Type { get; set; } = "Feature";

    public GeozoneProperties Properties { get; set; } = new();

    public GeozoneGeometry Geometry { get; set; } = new();
}

/// <summary>Everything the frontend's draw tools ever attach to a shape's properties —
/// <see cref="Radius"/> is only present for a drawn circle, left null for a rectangle/polygon.</summary>
public class GeozoneProperties
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Radius { get; set; }
}

public class GeozoneGeometry
{
    public string Type { get; set; } = string.Empty;

    [JsonConverter(typeof(GeozoneCoordinatesConverter))]
    public GeozoneCoordinates Coordinates { get; set; } = new();
}

/// <summary>
/// GeoJSON's "coordinates" field is shaped differently per geometry type — everything the
/// frontend's draw tools ever produce is either a Point (a drawn circle's center: a flat
/// [longitude, latitude] pair) or a Polygon (a drawn rectangle/polygon: rings of
/// [longitude, latitude] pairs, three levels deep). The two are mutually exclusive — exactly
/// one is populated, matching whichever shape <see cref="GeozoneGeometry.Type"/> names.
/// </summary>
public class GeozoneCoordinates
{
    public double[]? Point { get; set; }

    public double[][][]? Polygon { get; set; }
}

/// <summary>Serializes/deserializes GeozoneCoordinates as a bare GeoJSON coordinates array
/// rather than a wrapping {"point":...}/{"polygon":...} object — Point vs Polygon is told apart
/// on read by looking at the nesting depth of the first element. Ported from the frontend so the
/// wire format matches exactly.</summary>
public class GeozoneCoordinatesConverter : JsonConverter<GeozoneCoordinates>
{
    public override GeozoneCoordinates Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        // A Point's coordinates are a flat [lng, lat] pair — its first element is a number.
        // A Polygon's coordinates are nested three levels deep — its first element is an array.
        if (root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Number)
        {
            return new GeozoneCoordinates { Point = root.Deserialize<double[]>(options) };
        }

        return new GeozoneCoordinates { Polygon = root.Deserialize<double[][][]>(options) };
    }

    public override void Write(Utf8JsonWriter writer, GeozoneCoordinates value, JsonSerializerOptions options)
    {
        if (value.Point is not null)
        {
            JsonSerializer.Serialize(writer, value.Point, options);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.Polygon ?? [], options);
        }
    }
}
