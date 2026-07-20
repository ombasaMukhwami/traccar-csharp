using System.Text.RegularExpressions;

namespace Traccar.Protocols.Geocoder;

public sealed class AddressFormat(string? format = null)
{
    private const string DefaultFormat = "%h %r, %t, %s, %c";
    private readonly string _format = format ?? DefaultFormat;

    public string? Format(GeocoderAddress address)
    {
        var result = _format
            .Replace("%p", address.Postcode ?? "")
            .Replace("%c", address.Country ?? "")
            .Replace("%s", address.State ?? "")
            .Replace("%d", address.District ?? "")
            .Replace("%t", address.Settlement ?? "")
            .Replace("%u", address.Suburb ?? "")
            .Replace("%r", address.Street ?? "")
            .Replace("%h", address.House ?? "")
            .Replace("%f", address.FormattedAddress ?? "");

        // Split on commas, collapse internal whitespace, strip empty parts.
        var parts = result.Split(',')
            .Select(p => Regex.Replace(p, @"\s+", " ").Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        return parts.Length > 0 ? string.Join(", ", parts) : null;
    }
}
