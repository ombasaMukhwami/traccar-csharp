namespace Traccar.Protocols.Geocoder;

public interface IGeocoderService
{
    Task<string?> GetAddressAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
