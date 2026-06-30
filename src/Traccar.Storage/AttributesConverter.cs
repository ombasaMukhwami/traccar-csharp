using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Traccar.Storage;

public static class AttributesConverter
{
    public static readonly ValueConverter<Dictionary<string, object>, string> Converter = new(
        dictionary => JsonSerializer.Serialize(dictionary, (JsonSerializerOptions?)null),
        json => Deserialize(json));

    public static readonly ValueComparer<Dictionary<string, object>> Comparer = new(
        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
        dictionary => JsonSerializer.Serialize(dictionary, (JsonSerializerOptions?)null).GetHashCode(),
        dictionary => Deserialize(JsonSerializer.Serialize(dictionary, (JsonSerializerOptions?)null)));

    private static Dictionary<string, object> Deserialize(string json)
    {
        var result = new Dictionary<string, object>();
        if (string.IsNullOrEmpty(json) || json == "null")
        {
            return result;
        }
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = ToClrValue(property.Value);
        }
        return result;
    }

    private static object ToClrValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => element.GetString()!,
            _ => element.GetRawText(),
        };
    }
}
