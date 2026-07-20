namespace Traccar.Protocols.Geocoder;

public sealed record GeocoderAddress
{
    public string? Postcode { get; init; }
    public string? Country { get; init; }
    public string? State { get; init; }
    public string? District { get; init; }
    public string? Settlement { get; init; }
    public string? Suburb { get; init; }
    public string? Street { get; init; }
    public string? House { get; init; }
    public string? FormattedAddress { get; init; }
}
