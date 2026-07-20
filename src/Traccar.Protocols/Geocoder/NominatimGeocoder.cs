using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Traccar.Protocols.Geocoder;

public sealed class NominatimGeocoder(HttpClient httpClient, IConfiguration configuration) : IGeocoderService
{
    private const string UrlTemplate =
        "https://nominatim.openstreetmap.org/reverse?format=json&lat={0:F6}&lon={1:F6}&zoom=18&addressdetails=1";

    private readonly AddressFormat _addressFormat = new(configuration[ConfigKeys.Geocoder.Format]);
    private readonly int _cacheSize = configuration.GetValue(ConfigKeys.Geocoder.CacheSize, 512);
    private readonly ConcurrentDictionary<string, string?> _cache = new();

    public async Task<string?> GetAddressAsync(double latitude, double longitude,
        CancellationToken cancellationToken = default)
    {
        var key = FormattableString.Invariant($"{latitude:F6},{longitude:F6}");
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var url = string.Format(UrlTemplate, latitude, longitude);
        var json = await httpClient.GetStringAsync(url, cancellationToken);

        var address = ParseAddress(json);
        var formatted = _addressFormat.Format(address);

        // Simple capacity-bounded cache: clear when full (not true LRU, but avoids unbounded growth).
        if (_cacheSize > 0 && _cache.Count >= _cacheSize)
            _cache.Clear();

        _cache[key] = formatted;
        return formatted;
    }

    private static GeocoderAddress ParseAddress(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var formattedAddress = root.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;

        if (!root.TryGetProperty("address", out var addr))
            return new GeocoderAddress { FormattedAddress = formattedAddress };

        return new GeocoderAddress
        {
            House = GetString(addr, "house_number"),
            Street = GetString(addr, "road"),
            Suburb = GetString(addr, "suburb"),
            Settlement = GetFirstOf(addr, "village", "town", "city"),
            State = GetString(addr, "state"),
            District = GetFirstOf(addr, "state_district", "region"),
            Country = GetString(addr, "country_code")?.ToUpperInvariant(),
            Postcode = GetString(addr, "postcode"),
            FormattedAddress = formattedAddress,
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) ? val.GetString() : null;

    private static string? GetFirstOf(JsonElement element, params string[] properties)
    {
        foreach (var p in properties)
            if (element.TryGetProperty(p, out var val))
                return val.GetString();
        return null;
    }
}
