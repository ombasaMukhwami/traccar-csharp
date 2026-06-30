using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Traccar.Storage;

public static class JsonValueConverter<T>
    where T : class
{
    public static readonly ValueConverter<T?, string?> Converter = new(
        value => value == null ? null : JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
        json => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json, (JsonSerializerOptions?)null));
}
