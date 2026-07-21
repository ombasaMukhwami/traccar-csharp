namespace Traccar.Model;

/// <summary>Bulk-reassign a set of devices onto a different client.</summary>
public class MoveDevicesRequest
{
    public int FromClientId { get; set; }

    public int ToClientId { get; set; }

    public List<string> UniqueIds { get; set; } = [];
}
