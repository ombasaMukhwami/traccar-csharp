namespace Traccar.Model;

/// <summary>
/// Catalog of known tracker hardware models — drives the model dropdown when creating/editing a
/// device. Distinct from <see cref="Device.Model"/>, which is just the free-text value a device
/// row currently holds; this is the reference list it's picked from.
/// </summary>
public class DeviceModelInfo
{
    public int Id { get; set; }

    public string Model { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string? Port { get; set; }

    /// <summary>Seed catalog of known tracker hardware.</summary>
    public static readonly IReadOnlyList<DeviceModelInfo> Catalog =
    [
        new() { Id = 1, Model = "TK-103", Manufacturer = "Xexun", Port = "COM1" },
        new() { Id = 2, Model = "GT-06", Manufacturer = "Concox", Port = "COM2" },
        new() { Id = 3, Model = "Concox JM01", Manufacturer = "Concox", Port = "COM3" },
        new() { Id = 4, Model = "Teltonika FMB920", Manufacturer = "Teltonika", Port = "COM4" },
    ];
}
