namespace Traccar.Model;

public class Message : ExtendedModel
{
    public long DeviceId { get; set; }

    public string? Type { get; set; }
}
